using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>Đơn cọc VietQR trả cho web/App: kèm mã booking, hạn giữ phòng mới nhất và cờ demo.</summary>
public sealed record DepositOrderView(
    string OrderId, long BookingId, string BookingCode, decimal Amount, string Description,
    string QrCode, string QrDataUrl, string Status, string? TransactionId,
    DateTime CreatedAt, DateTime? PaidAt, DateTime? HoldExpiresAt, bool Demo);

/// <summary>
/// Thanh toán cọc bằng VietQR cho booking đang giữ chỗ (HOLD).
/// Luồng: khách tạo QR → chuyển khoản → hệ thống tra cứu VietQR (web hỏi mỗi 5 giây, job nền hỏi mỗi phút,
/// nhân viên bấm "Kiểm tra ngay") → nhận đủ tiền khi phòng còn giữ thì booking chuyển BOOKED;
/// nhận tiền khi đã hết giữ phòng hoặc thiếu tiền thì ghi phiếu cọc + phiếu hoàn chờ xử lý.
/// </summary>
public sealed class DepositPaymentService(HotelDbContext db, CurrentActor actor, BookingService bookings,
    IVietQrGateway gateway, IOptions<VietQrSettings> options, NotificationWriter notifications)
{
    private static readonly string[] FinalStatuses = ["PAID", "REFUND_REQUIRED", "REFUNDED"];

    /// <summary>
    /// Tạo QR cọc (khách chủ booking). Đã có QR đang chờ thì trả lại QR đó;
    /// QR cũ đã thất bại/hết hạn phía VietQR thì tạo QR mới thay thế.
    /// Khi tạo QR, thời gian giữ phòng được nới để còn ít nhất HoldExtensionMinutes phút.
    /// </summary>
    public async Task<DepositPaymentOrder> CreateAsync(long bookingId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {bookingId} FOR UPDATE").ToListAsync(ct);
        var booking = await db.DatPhongs.SingleOrDefaultAsync(x => x.Id == bookingId && x.KhachHang.TaiKhoanId == actor.AccountId, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
        var order = await db.Set<DepositPaymentOrder>().SingleOrDefaultAsync(x => x.BookingId == bookingId, ct);
        if (order is not null && order.Status != "FAILED") return order;
        if (booking.TrangThai != "HOLD") throw new InvalidArgumentException("Only a held booking can request a deposit QR.");
        var holds = await db.KhoaLichPhongs.Where(x => db.ChiTietDatPhongs.Any(d => d.Id == x.ChiTietDatPhongId && d.DatPhongId == bookingId)).ToListAsync(ct);
        if (holds.Count == 0 || holds.Any(x => x.TrangThai != "HOLD" || x.HetHanGiuCho <= DateTime.UtcNow)) throw new InvalidArgumentException("The booking hold expired.");
        var amount = decimal.Ceiling(await bookings.RequiredDepositAsync(booking, ct));
        if (amount <= 0 || amount > booking.TongTienDuKien) throw new InvalidArgumentException("No payable whole-VND deposit amount is available.");

        var id = Guid.NewGuid().ToString("N");
        // Nội dung ngắn (app ngân hàng giới hạn ~25 ký tự, không dấu): COC<id booking> <8 ký tự đầu mã đơn>.
        var description = $"COC{bookingId} {id[..8].ToUpperInvariant()}";
        var s = options.Value;
        var qr = await gateway.CreateCustomerQrAsync(new(decimal.ToInt64(amount), description, s.BankAccount, s.BankCode, s.UserBankName, "C", id, 0), ct);
        var now = DateTime.UtcNow;
        if (order is null)
        {
            order = new DepositPaymentOrder { BookingId = bookingId };
            db.Add(order);
        }
        order.OrderId = id; order.AccountId = actor.AccountId; order.Amount = amount; order.Description = description;
        order.QrCode = qr.QrCode; order.QrDataUrl = qr.QrDataUrl; order.Status = "PENDING";
        order.TransactionId = null; order.PaidAt = null; order.CreatedAt = now;

        // Khách cần thời gian mở app ngân hàng: giữ phòng còn ít nhất N phút kể từ lúc có QR.
        var minExpiry = now.AddMinutes(Math.Clamp(s.HoldExtensionMinutes, 0, 60));
        foreach (var hold in holds.Where(h => h.HetHanGiuCho < minExpiry)) hold.HetHanGiuCho = minExpiry;

        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }

    /// <summary>Tra cứu VietQR cho một đơn (khách chủ đơn hoặc nhân viên có BILLING_PAYMENT_WRITE).</summary>
    public async Task<DepositPaymentOrder> InquireAsync(string orderId, CancellationToken ct)
        => await CheckGatewayAsync(await FindAsync(orderId, ct), ct);

    /// <summary>
    /// Chế độ demo: ghi nhận như thể ngân hàng đã báo nhận đủ tiền (mã giao dịch bắt đầu bằng DEMO).
    /// Bị từ chối khi đang dùng VietQR thật.
    /// </summary>
    public async Task<DepositPaymentOrder> SimulatePaidAsync(string orderId, CancellationToken ct)
    {
        if (!options.Value.IsDemo) throw new InvalidArgumentException("Simulated payment is only available in VietQR demo mode.");
        var order = await FindAsync(orderId, ct);
        if (FinalStatuses.Contains(order.Status)) return order;
        if (order.Status != "PENDING") throw new InvalidArgumentException("The QR is no longer valid; create a new QR.");
        var txn = $"DEMO{DateTime.UtcNow:yyMMddHHmmss}{order.OrderId[..6].ToUpperInvariant()}";
        return await ConfirmAsync(order, order.Amount, txn, ct);
    }

    /// <summary>Đơn đang chờ tạo trong 24 giờ qua để job nền tra cứu (không chạy ở chế độ demo).</summary>
    public async Task<List<long>> PendingOrderIdsAsync(int max, CancellationToken ct)
    {
        if (options.Value.IsDemo) return [];
        var since = DateTime.UtcNow.AddHours(-24);
        return await db.Set<DepositPaymentOrder>().AsNoTracking().Where(x => x.Status == "PENDING" && x.CreatedAt >= since)
            .OrderBy(x => x.Id).Take(max).Select(x => x.Id).ToListAsync(ct);
    }

    /// <summary>Job nền: tra cứu một đơn theo id (không cần người đăng nhập).</summary>
    public async Task<DepositPaymentOrder> CheckByIdAsync(long id, CancellationToken ct)
        => await CheckGatewayAsync(await db.Set<DepositPaymentOrder>().SingleAsync(x => x.Id == id, ct), ct);

    /// <summary>Ghép thông tin hiển thị: mã booking, hạn giữ phòng hiện tại, cờ demo.</summary>
    public async Task<DepositOrderView> ViewAsync(DepositPaymentOrder order, CancellationToken ct)
    {
        var code = await db.DatPhongs.Where(x => x.Id == order.BookingId).Select(x => x.MaDatPhong).SingleAsync(ct);
        var hold = await db.KhoaLichPhongs.Where(k => db.ChiTietDatPhongs.Any(d => d.Id == k.ChiTietDatPhongId && d.DatPhongId == order.BookingId))
            .Select(k => k.HetHanGiuCho).MaxAsync(ct);
        return new DepositOrderView(order.OrderId, order.BookingId, code, order.Amount, order.Description, order.QrCode, order.QrDataUrl,
            order.Status, order.TransactionId, Utc(order.CreatedAt), order.PaidAt is { } p ? Utc(p) : null, hold is { } h ? Utc(h) : null,
            options.Value.IsDemo);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private async Task<DepositPaymentOrder> FindAsync(string orderId, CancellationToken ct)
    {
        var order = await db.Set<DepositPaymentOrder>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (order is null || (order.AccountId != actor.AccountId && !await actor.HasPermissionAsync("BILLING_PAYMENT_WRITE", ct)))
            throw new ResourceNotFoundException("Deposit order was not found.");
        return order;
    }

    private async Task<DepositPaymentOrder> CheckGatewayAsync(DepositPaymentOrder order, CancellationToken ct)
    {
        if (FinalStatuses.Contains(order.Status)) return order;
        var result = await gateway.InquireAsync(order.OrderId, ct);
        if (result.Code != "00" || result.OrderId != order.OrderId) throw new ServiceUnavailableException("Gateway result does not match the stored deposit order.");
        var status = result.Status.ToUpperInvariant();
        if (status is not ("SUCCESS" or "PAID"))
        {
            if (status is "FAILED" or "CANCELLED" or "EXPIRED" && order.Status != "FAILED")
            {
                order.Status = "FAILED"; await db.SaveChangesAsync(ct);
            }
            return order;
        }
        if (string.IsNullOrWhiteSpace(result.TransactionId)) throw new ServiceUnavailableException("Successful payment is missing a transaction reference.");
        if (result.Amount <= 0) throw new ServiceUnavailableException("Successful payment is missing the received amount.");
        return await ConfirmAsync(order, result.Amount, result.TransactionId, ct);
    }

    /// <summary>
    /// Ghi nhận tiền đã nhận (một lần duy nhất cho mỗi mã giao dịch).
    /// Đủ tiền (không vượt tổng booking) và phòng còn giữ → phiếu cọc + booking BOOKED.
    /// Ngược lại → phiếu cọc + phiếu hoàn CHO_XU_LY, đơn chuyển REFUND_REQUIRED.
    /// </summary>
    private async Task<DepositPaymentOrder> ConfirmAsync(DepositPaymentOrder order, decimal received, string transactionId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational())
        {
            // Cùng thứ tự khóa booking -> đơn như CreateAsync và thu cọc tại quầy.
            await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {order.BookingId} FOR UPDATE").ToListAsync(ct);
            await db.Entry(order).ReloadAsync(ct);
        }
        if (FinalStatuses.Contains(order.Status)) return order;
        if (await db.PhieuCocs.AnyAsync(x => x.CongThanhToan == "VietQR" && x.MaGiaoDich == transactionId, ct) || await db.ThanhToanHoaDonPhongs.AnyAsync(x => x.CongThanhToan == "VietQR" && x.MaGiaoDich == transactionId, ct)) throw new AlreadyExistsException("This bank transaction was already recorded.");
        var method = await db.PhuongThucThanhToans.SingleOrDefaultAsync(x => x.MaPhuongThuc == "VIETQR" && x.TrangThai == "HOAT_DONG", ct) ?? throw new InvalidArgumentException("VIETQR payment method is not configured.");
        var booking = await db.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleAsync(x => x.Id == order.BookingId, ct);
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray(); var holds = await db.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        var validHold = booking.TrangThai == "HOLD" && holds.Count > 0 && holds.All(x => x.TrangThai == "HOLD" && x.HetHanGiuCho > DateTime.UtcNow);
        var validAmount = received >= order.Amount && received <= booking.TongTienDuKien;
        var accepted = validHold && validAmount;

        var deposit = new PhieuCoc { MaPhieuCoc = $"PC{Guid.NewGuid():N}", DatPhongId = booking.Id, TaiKhoanThanhToanId = order.AccountId, PhuongThucId = method.Id, SoTien = received,
            CongThanhToan = "VietQR", MaGiaoDich = transactionId, ThoiGianCoc = DateTime.UtcNow, TrangThai = "DA_THANH_TOAN" };
        db.Add(deposit); order.TransactionId = transactionId; order.PaidAt = DateTime.UtcNow; order.Status = accepted ? "PAID" : "REFUND_REQUIRED";
        var oldStatus = booking.TrangThai;
        if (accepted)
        {
            booking.TrangThai = "BOOKED"; booking.UpdatedAt = DateTime.UtcNow;
            foreach (var d in booking.ChiTietDatPhongs) d.TrangThai = "BOOKED";
            foreach (var h in holds) { h.TrangThai = "BOOKED"; h.HetHanGiuCho = null; }
        }
        await db.SaveChangesAsync(ct);

        string message;
        if (accepted) message = "Đã nhận cọc VietQR và xác nhận đặt phòng.";
        else
        {
            var reason = !validHold
                ? "Nhận tiền cọc sau khi hết thời gian giữ phòng"
                : string.Format(CultureInfo.InvariantCulture, "Số tiền chuyển {0:#,0} không khớp tiền cọc {1:#,0}", received, order.Amount);
            db.Add(new PhieuHoanTien { MaPhieuHoan = $"HT{Guid.NewGuid():N}", PhieuCocId = deposit.Id, PhuongThucId = method.Id, SoTien = received, LyDo = reason, TrangThai = "CHO_XU_LY", ThoiGianHoan = DateTime.UtcNow });
            message = $"{reason}; yêu cầu hoàn tiền đã được tạo.";
        }
        await notifications.BookingAsync(booking, order.AccountId, oldStatus, message, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }
}
