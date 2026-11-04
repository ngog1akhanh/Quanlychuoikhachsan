using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Common;

public sealed class NotificationWriter(HotelDbContext db)
{
    // Caller owns the transaction. Snapshot dates are never overwritten later.
    public async Task BookingAsync(DatPhong booking, long? accountId, string oldStatus, string reason, CancellationToken ct)
    {
        db.Add(new LichSuTrangThaiDatPhong { DatPhongId = booking.Id, TrangThaiCu = oldStatus, TrangThaiMoi = booking.TrangThai,
            TaiKhoanThucHienId = accountId, LyDo = reason, CreatedAt = DateTime.UtcNow });
        var customerAccount = await db.KhachHangs.Where(x => x.Id == booking.KhachHangId).Select(x => x.TaiKhoanId).SingleAsync(ct);
        var recipients = await db.TaiKhoanVaiTros.Where(x => x.TaiKhoan.TrangThai == "HOAT_DONG" && x.VaiTro.TrangThai == "HOAT_DONG" && x.VaiTro.VaiTroQuyens.Any(p => p.Quyen.MaQuyen == "BOOKING_READ"))
            .Select(x => x.TaiKhoanId).Distinct().ToListAsync(ct);
        if (customerAccount.HasValue) recipients.Add(customerAccount.Value);
        var level = booking.TrangThai is "CHO_DUYET_HUY" or "HET_HAN" or "NO_SHOW" ? "QUAN_TRONG" : "THONG_THUONG";
        await SendAsync("BOOKING", $"Đặt phòng {booking.MaDatPhong}: {StatusLabel(booking.TrangThai)}", reason, recipients.Distinct().ToArray(), accountId, booking, ct, level);
    }

    /// <summary>Nhãn tiếng Việt của trạng thái booking để tiêu đề thông báo dễ đọc.</summary>
    public static string StatusLabel(string status) => status switch
    {
        "HOLD" => "Đang giữ phòng",
        "CHO_COC" => "Chờ đặt cọc",
        "BOOKED" => "Đã xác nhận",
        "DANG_O" => "Khách đang ở",
        "DA_TRA" => "Đã trả phòng",
        "CHO_DUYET_HUY" => "Chờ duyệt hủy",
        "DA_HUY" => "Đã hủy",
        "NO_SHOW" => "Khách không đến",
        "HET_HAN" => "Hết hạn giữ phòng",
        _ => status
    };
    /// <param name="level">THONG_THUONG hoặc QUAN_TRONG (app tô đỏ và hiện popup nổi bật).</param>
    public async Task SendAsync(string type, string title, string content, IReadOnlyList<long> recipients, long? creatorId, DatPhong? booking, CancellationToken ct,
        string level = "THONG_THUONG")
    {
        if (title.Length > 255) title = title[..252] + "...";
        if (content.Length > 3000) content = content[..2997] + "...";
        var x = new ThongBao { LoaiThongBao = type, TieuDe = title, NoiDung = content, TaiKhoanTaoId = creatorId,
            DatPhongId = booking?.Id, CheckInLuc = booking?.DuKienNhan, CheckOutLuc = booking?.DuKienTra,
            MucDo = level, CreatedAt = DateTime.UtcNow, DuongDan = booking is null ? null : $"/bookings/{booking.Id}" };
        db.Add(x); await db.SaveChangesAsync(ct);
        var settings = await db.Set<CaiDatThongBao>().Where(s => recipients.Contains(s.TaiKhoanId)).ToListAsync(ct);
        foreach (var id in recipients.Distinct())
        {
            var s = settings.SingleOrDefault(s => s.TaiKhoanId == id);
            if (s is not null && !(type.StartsWith("BOOKING", StringComparison.Ordinal) ? s.NhanBooking : type == "PAYMENT" ? s.NhanThanhToan : s.NhanVanHanh)) continue;
            db.Add(new ThongBaoTaiKhoan { ThongBaoId = x.Id, TaiKhoanId = id, KenhGui = "APP", TrangThaiGui = "DA_GUI", GuiLuc = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(ct);
    }
}
