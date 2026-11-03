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
        await SendAsync("BOOKING", $"Đặt phòng {booking.MaDatPhong}: {booking.TrangThai}", reason, recipients.Distinct().ToArray(), accountId, booking, ct);
    }
    public async Task SendAsync(string type, string title, string content, IReadOnlyList<long> recipients, long? creatorId, DatPhong? booking, CancellationToken ct)
    {
        var x = new ThongBao { LoaiThongBao = type, TieuDe = title, NoiDung = content, TaiKhoanTaoId = creatorId,
            DatPhongId = booking?.Id, CheckInLuc = booking?.DuKienNhan, CheckOutLuc = booking?.DuKienTra,
            MucDo = "THONG_THUONG", CreatedAt = DateTime.UtcNow, DuongDan = booking is null ? null : $"/bookings/{booking.Id}" };
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
