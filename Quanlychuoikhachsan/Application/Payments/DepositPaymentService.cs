using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Payments;

public sealed class DepositPaymentService(HotelDbContext db, CurrentActor actor, BookingService bookings,
    IVietQrGateway gateway, IOptions<VietQrSettings> options, NotificationWriter notifications)
{
    public async Task<DepositPaymentOrder> CreateAsync(long bookingId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {bookingId} FOR UPDATE").ToListAsync(ct);
        var booking = await db.DatPhongs.SingleOrDefaultAsync(x => x.Id == bookingId && x.KhachHang.TaiKhoanId == actor.AccountId, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
        var old = await db.Set<DepositPaymentOrder>().SingleOrDefaultAsync(x => x.BookingId == bookingId, ct);
        if (old is not null) return old;
        if (booking.TrangThai != "HOLD") throw new InvalidArgumentException("Only a held booking can request a deposit QR.");
        var holds = await db.KhoaLichPhongs.Where(x => db.ChiTietDatPhongs.Any(d => d.Id == x.ChiTietDatPhongId && d.DatPhongId == bookingId)).ToListAsync(ct);
        if (holds.Count == 0 || holds.Any(x => x.TrangThai != "HOLD" || x.HetHanGiuCho <= DateTime.UtcNow)) throw new InvalidArgumentException("The booking hold expired.");
        var amount = decimal.Ceiling(await bookings.RequiredDepositAsync(booking, ct));
        if (amount <= 0 || amount > booking.TongTienDuKien) throw new InvalidArgumentException("No payable whole-VND deposit amount is available.");
        var id = Guid.NewGuid().ToString("N"); var description = $"COC{bookingId} {id}";
        var qr = await gateway.CreateCustomerQrAsync(new(decimal.ToInt64(amount), description, options.Value.BankAccount, options.Value.BankCode, options.Value.UserBankName, "C", id, 0), ct);
        var order = new DepositPaymentOrder { OrderId = id, BookingId = bookingId, AccountId = actor.AccountId, Amount = amount, Description = description, QrCode = qr.QrCode, QrDataUrl = qr.QrDataUrl, CreatedAt = DateTime.UtcNow };
        db.Add(order); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }
    public async Task<DepositPaymentOrder> InquireAsync(string orderId, CancellationToken ct)
    {
        var order = await db.Set<DepositPaymentOrder>().SingleOrDefaultAsync(x => x.OrderId == orderId && x.AccountId == actor.AccountId, ct) ?? throw new ResourceNotFoundException("Deposit order was not found.");
        if (order.Status is "PAID" or "REFUND_REQUIRED" or "REFUNDED") return order;
        var result = await gateway.InquireAsync(orderId, ct);
        if (result.Code != "00" || result.OrderId != orderId || result.Amount != order.Amount) throw new ServiceUnavailableException("Gateway result does not match the stored deposit order.");
        if (!string.Equals(result.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) && !string.Equals(result.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            if (result.Status.ToUpperInvariant() is "FAILED" or "CANCELLED" or "EXPIRED")
            {
                order.Status = "FAILED"; await db.SaveChangesAsync(ct);
            }
            return order;
        }
        if (string.IsNullOrWhiteSpace(result.TransactionId)) throw new ServiceUnavailableException("Successful payment is missing a transaction reference.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational())
        {
            // Same booking -> order lock order as CreateAsync and manual deposit confirmation.
            await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {order.BookingId} FOR UPDATE").ToListAsync(ct);
            await db.Entry(order).ReloadAsync(ct);
        }
        if (order.Status is "PAID" or "REFUND_REQUIRED" or "REFUNDED") return order;
        if (await db.PhieuCocs.AnyAsync(x => x.CongThanhToan == "VietQR" && x.MaGiaoDich == result.TransactionId, ct) || await db.ThanhToanHoaDonPhongs.AnyAsync(x => x.CongThanhToan == "VietQR" && x.MaGiaoDich == result.TransactionId, ct)) throw new AlreadyExistsException("This bank transaction was already recorded.");
        var method = await db.PhuongThucThanhToans.SingleOrDefaultAsync(x => x.MaPhuongThuc == "VIETQR" && x.TrangThai == "HOAT_DONG", ct) ?? throw new InvalidArgumentException("VIETQR payment method is not configured.");
        var booking = await db.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleAsync(x => x.Id == order.BookingId, ct);
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray(); var holds = await db.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        var validHold = booking.TrangThai == "HOLD" && holds.Count > 0 && holds.All(x => x.TrangThai == "HOLD" && x.HetHanGiuCho > DateTime.UtcNow);
        var deposit = new PhieuCoc { MaPhieuCoc = $"PC{Guid.NewGuid():N}", DatPhongId = booking.Id, TaiKhoanThanhToanId = actor.AccountId, PhuongThucId = method.Id, SoTien = order.Amount,
            CongThanhToan = "VietQR", MaGiaoDich = result.TransactionId, ThoiGianCoc = DateTime.UtcNow, TrangThai = "DA_THANH_TOAN" };
        db.Add(deposit); order.TransactionId = result.TransactionId; order.PaidAt = DateTime.UtcNow; order.Status = validHold ? "PAID" : "REFUND_REQUIRED";
        var oldStatus = booking.TrangThai;
        if (validHold)
        {
            booking.TrangThai = "BOOKED"; booking.UpdatedAt = DateTime.UtcNow;
            foreach (var d in booking.ChiTietDatPhongs) d.TrangThai = "BOOKED";
            foreach (var h in holds) { h.TrangThai = "BOOKED"; h.HetHanGiuCho = null; }
        }
        await db.SaveChangesAsync(ct);
        if (!validHold) db.Add(new PhieuHoanTien { MaPhieuHoan = $"HT{Guid.NewGuid():N}", PhieuCocId = deposit.Id, PhuongThucId = method.Id, SoTien = order.Amount, LyDo = "Deposit received after the booking hold became unavailable", TrangThai = "CHO_XU_LY", ThoiGianHoan = DateTime.UtcNow });
        await notifications.BookingAsync(booking, actor.AccountId, oldStatus, validHold ? "Đã nhận cọc VietQR và xác nhận đặt phòng." : "Đã nhận tiền sau khi hết giữ phòng; yêu cầu hoàn tiền đã được tạo.", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }
}
