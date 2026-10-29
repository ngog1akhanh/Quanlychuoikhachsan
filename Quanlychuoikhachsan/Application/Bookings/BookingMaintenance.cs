using System.Data;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed class BookingMaintenance(HotelDbContext db, NotificationWriter notifications)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.DatPhongs.Where(b => b.TrangThai == "HOLD" && !db.KhoaLichPhongs.Any(k => db.ChiTietDatPhongs.Any(d => d.Id == k.ChiTietDatPhongId && d.DatPhongId == b.Id) && k.HetHanGiuCho > now))
            .OrderBy(b => b.Id).Take(100).Select(b => b.Id).ToListAsync(ct);
        foreach (var id in candidates)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsRelational()) await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {id} FOR UPDATE").ToListAsync(ct);
            var booking = await db.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleAsync(x => x.Id == id, ct);
            var detailIds = booking.ChiTietDatPhongs.Select(d => d.Id).ToArray(); var holds = await db.KhoaLichPhongs.Where(k => detailIds.Contains(k.ChiTietDatPhongId)).ToListAsync(ct);
            if (booking.TrangThai != "HOLD" || holds.Any(k => k.HetHanGiuCho > now)) continue;
            db.RemoveRange(holds); booking.TrangThai = "HET_HAN"; booking.UpdatedAt = now;
            foreach (var d in booking.ChiTietDatPhongs) d.TrangThai = "HET_HAN";
            await notifications.BookingAsync(booking, null, "HOLD", "Hết thời gian giữ phòng.", ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        var upcoming = await db.DatPhongs.Include(b => b.KhachHang).Where(b => b.KhachHang.TaiKhoanId.HasValue &&
            ((b.TrangThai == "BOOKED" && b.DuKienNhan > now && b.DuKienNhan <= now.AddDays(1)) || (b.TrangThai == "DANG_O" && b.DuKienTra > now && b.DuKienTra <= now.AddDays(1))) &&
            !db.Set<ThongBao>().Any(n => n.DatPhongId == b.Id && n.LoaiThongBao == (b.TrangThai == "BOOKED" ? "BOOKING_ARRIVAL_REMINDER" : "BOOKING_DEPARTURE_REMINDER") && n.CheckInLuc == b.DuKienNhan && n.CheckOutLuc == b.DuKienTra))
            .OrderBy(b => b.Id).Take(100).ToListAsync(ct);
        foreach (var booking in upcoming.Where(b => b.KhachHang.TaiKhoanId.HasValue))
        {
            var type = booking.TrangThai == "BOOKED" ? "BOOKING_ARRIVAL_REMINDER" : "BOOKING_DEPARTURE_REMINDER";
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.Set<ThongBao>().AnyAsync(n => n.DatPhongId == booking.Id && n.LoaiThongBao == type && n.CheckInLuc == booking.DuKienNhan && n.CheckOutLuc == booking.DuKienTra, ct)) continue;
            await notifications.SendAsync(type, "Nhắc lịch lưu trú", $"Lịch lưu trú của đặt phòng {booking.MaDatPhong} sắp đến.", [booking.KhachHang.TaiKhoanId!.Value], null, booking, ct); await tx.CommitAsync(ct);
        }
        await NotifyOperationsAsync(now, ct);
    }

    private async Task NotifyOperationsAsync(DateTime now, CancellationToken ct)
    {
        var tasks = await db.CongViecBuongPhongs.Where(x => x.TrangThai == "CHO_XU_LY").OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        if (tasks.Count > 0) await NotifyStaffAsync("HOUSEKEEPING", "HOUSEKEEPING_READ", "Công việc buồng phòng đang chờ", string.Join(",", tasks), now, ct);
        var incidents = await db.TinhTrangPhongs.Where(x => x.TrangThaiXuLy != "DA_XU_LY" && x.MucDo == "NGHIEM_TRONG").OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        if (incidents.Count > 0) await NotifyStaffAsync("MAINTENANCE", "HOUSEKEEPING_READ", "Sự cố phòng cần xử lý", string.Join(",", incidents), now, ct);
        var warehouses = await db.Khos.Where(x => x.TrangThai == "HOAT_DONG").Select(x => x.Id).ToListAsync(ct);
        foreach (var warehouse in warehouses)
        {
            var low = await db.DanhMucHangHoas.Where(x => x.TrangThai == "HOAT_DONG" && x.MucTonToiThieu > 0 &&
                (db.GiaoDichKhos.Where(g => g.KhoId == warehouse && g.DanhMucHangHoaId == x.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0) < x.MucTonToiThieu)
                .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
            if (low.Count > 0) await NotifyStaffAsync("LOW_STOCK", "INVENTORY_READ", $"Kho {warehouse}: hàng dưới tồn tối thiểu", string.Join(",", low), now, ct);
        }
    }

    private async Task NotifyStaffAsync(string type, string permission, string title, string ids, DateTime now, CancellationToken ct)
    {
        // A digest changes when the outstanding set changes; unchanged sets notify at most once per day.
        var content = $"Các mã cần kiểm tra: {ids}";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Set<ThongBao>().AnyAsync(x => x.LoaiThongBao == type && x.TieuDe == title && x.NoiDung == content && x.CreatedAt >= now.Date, ct)) return;
        var recipients = await db.TaiKhoanVaiTros.Where(x => x.TaiKhoan.TrangThai == "HOAT_DONG" && x.VaiTro.TrangThai == "HOAT_DONG" && x.VaiTro.VaiTroQuyens.Any(p => p.Quyen.MaQuyen == permission)).Select(x => x.TaiKhoanId).Distinct().ToListAsync(ct);
        if (recipients.Count == 0) return;
        await notifications.SendAsync(type, title, content, recipients, null, null, ct); await tx.CommitAsync(ct);
    }
}

public sealed class BookingMaintenanceWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<BookingMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BookingMaintenance:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<BookingMaintenance>().RunAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Booking expiration/reminder maintenance failed."); }
        }
    }
}
