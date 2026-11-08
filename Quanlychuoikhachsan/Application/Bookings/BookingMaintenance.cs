using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;
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

    /// <summary>
    /// Thông báo vận hành định kỳ cho nhân viên (gộp theo loại, mỗi nội dung tối đa 1 lần/ngày).
    /// Nội dung ghi số phòng / tên hàng để người đọc hiểu ngay, không ghi mã id.
    /// </summary>
    private async Task NotifyOperationsAsync(DateTime now, CancellationToken ct)
    {
        // 1. Việc buồng phòng đang chờ
        var pendingRooms = await (from t in db.CongViecBuongPhongs
                                  join p in db.Phongs on t.PhongId equals p.Id
                                  where t.TrangThai == "CHO_XU_LY"
                                  select p.SoPhong).ToListAsync(ct);
        if (pendingRooms.Count > 0)
            await NotifyStaffAsync("HOUSEKEEPING", "HOUSEKEEPING_READ", "Công việc buồng phòng đang chờ",
                $"{pendingRooms.Count} việc chưa bắt đầu. Phòng: {RoomList(pendingRooms)}.", "THONG_THUONG", now, ct);

        // 2. Sự cố phòng nghiêm trọng chưa xử lý xong
        var incidents = await (from x in db.TinhTrangPhongs
                               join p in db.Phongs on x.PhongId equals p.Id
                               where x.TrangThaiXuLy != "DA_XU_LY" && x.MucDo == "NGHIEM_TRONG"
                               select new { p.SoPhong, x.LoaiTinhTrang }).ToListAsync(ct);
        if (incidents.Count > 0)
            await NotifyStaffAsync("MAINTENANCE", "HOUSEKEEPING_READ", "Sự cố phòng nghiêm trọng cần xử lý",
                string.Join("; ", incidents.OrderBy(x => x.SoPhong).Select(x => $"Phòng {x.SoPhong}: {x.LoaiTinhTrang}")) + ".", "QUAN_TRONG", now, ct);

        // 3. Khách đang ở đã quá giờ trả phòng dự kiến
        var overdue = await (from d in db.ChiTietDatPhongs
                             join p in db.Phongs on d.PhongId equals p.Id
                             where d.TrangThai == "DANG_O" && d.DatPhong.DuKienTra < now
                             select p.SoPhong).ToListAsync(ct);
        if (overdue.Count > 0)
            await NotifyStaffAsync("BOOKING_OVERDUE", "BOOKING_READ", "Khách quá giờ trả phòng",
                $"Phòng: {RoomList(overdue)}. Gia hạn hoặc làm thủ tục trả phòng.", "QUAN_TRONG", now, ct);

        // 4 + 5. Theo từng kho: hàng dưới tồn tối thiểu, lô sắp hết hạn / đã hết hạn còn tồn
        var today = HotelTime.Today;
        var expiryLimit = today.AddDays(7);
        var warehouses = await db.Khos.Where(x => x.TrangThai == "HOAT_DONG").OrderBy(x => x.Id).Select(x => new { x.Id, x.TenKho }).ToListAsync(ct);
        foreach (var warehouse in warehouses)
        {
            // Chỉ xét mặt hàng đã từng có giao dịch ở kho này, tránh báo mọi mặt hàng cho kho mới/trống.
            var low = await db.DanhMucHangHoas
                .Where(x => x.TrangThai == "HOAT_DONG" && x.MucTonToiThieu > 0
                    && db.GiaoDichKhos.Any(g => g.KhoId == warehouse.Id && g.DanhMucHangHoaId == x.Id))
                .Select(x => new { x.TenHang, x.MucTonToiThieu,
                    Balance = db.GiaoDichKhos.Where(g => g.KhoId == warehouse.Id && g.DanhMucHangHoaId == x.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0 })
                .ToListAsync(ct);
            var lowItems = low.Where(x => x.Balance < x.MucTonToiThieu).OrderBy(x => x.TenHang).ToList();
            if (lowItems.Count > 0)
                await NotifyStaffAsync("LOW_STOCK", "INVENTORY_READ", $"{warehouse.TenKho}: hàng dưới tồn tối thiểu",
                    string.Join("; ", lowItems.Select(x => $"{x.TenHang} còn {Qty(x.Balance)}/{Qty(x.MucTonToiThieu)}")) + ".", "THONG_THUONG", now, ct);

            var lots = await (from l in db.LoHangs
                              join h in db.DanhMucHangHoas on l.DanhMucHangHoaId equals h.Id
                              where l.KhoId == warehouse.Id && l.HanSuDung != null && l.HanSuDung <= expiryLimit
                              select new { l.MaLo, l.HanSuDung, h.TenHang,
                                  Balance = db.GiaoDichKhos.Where(g => g.LoHangId == l.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0 })
                             .ToListAsync(ct);
            var expiring = lots.Where(x => x.Balance > 0).OrderBy(x => x.HanSuDung).ToList();
            if (expiring.Count > 0)
            {
                var anyExpired = expiring.Any(x => x.HanSuDung!.Value < today);
                var content = string.Join("; ", expiring.Select(x =>
                {
                    var days = x.HanSuDung!.Value.DayNumber - today.DayNumber;
                    var when = days < 0 ? $"đã hết hạn {-days} ngày" : days == 0 ? "hết hạn hôm nay" : $"còn {days} ngày";
                    return $"{x.TenHang} lô {x.MaLo}: {when}, tồn {Qty(x.Balance)}";
                })) + ".";
                await NotifyStaffAsync("EXPIRY", "INVENTORY_READ", $"{warehouse.TenKho}: hàng sắp hết hạn / đã hết hạn",
                    content, anyExpired ? "QUAN_TRONG" : "THONG_THUONG", now, ct);
            }
        }
    }

    private static string RoomList(IEnumerable<string> rooms) => string.Join(", ", rooms.Distinct().OrderBy(x => x));
    private static string Qty(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private async Task NotifyStaffAsync(string type, string permission, string title, string content, string level, DateTime now, CancellationToken ct)
    {
        // Nội dung chỉ đổi khi danh sách việc/hàng đổi; nội dung giống nhau chỉ gửi tối đa 1 lần mỗi ngày.
        if (content.Length > 3000) content = content[..2997] + "...";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Set<ThongBao>().AnyAsync(x => x.LoaiThongBao == type && x.TieuDe == title && x.NoiDung == content && x.CreatedAt >= now.Date, ct)) return;
        var recipients = await db.TaiKhoanVaiTros.Where(x => x.TaiKhoan.TrangThai == "HOAT_DONG" && x.VaiTro.TrangThai == "HOAT_DONG" && x.VaiTro.VaiTroQuyens.Any(p => p.Quyen.MaQuyen == permission)).Select(x => x.TaiKhoanId).Distinct().ToListAsync(ct);
        if (recipients.Count == 0) return;
        await notifications.SendAsync(type, title, content, recipients, null, null, ct, level); await tx.CommitAsync(ct);
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
            // Kiểm tra đơn cọc VietQR trước, để tiền đã chuyển kịp xác nhận trước khi booking bị hủy vì hết giữ chỗ.
            try { await CheckDepositPaymentsAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "VietQR deposit check failed."); }
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<BookingMaintenance>().RunAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Booking expiration/reminder maintenance failed."); }
        }
    }

    // Tra cứu VietQR cho tối đa 20 đơn cọc đang chờ (tạo trong 24 giờ). Mỗi đơn một scope riêng để lỗi của đơn này
    // không làm hỏng dữ liệu đang theo dõi của đơn khác. Chế độ demo không có đơn nào được trả về.
    private async Task CheckDepositPaymentsAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var scope = scopes.CreateAsyncScope())
            ids = await scope.ServiceProvider.GetRequiredService<DepositPaymentService>().PendingOrderIdsAsync(20, ct);
        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DepositPaymentService>().CheckByIdAsync(id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not check VietQR deposit order {Id}.", id); }
        }
    }
}
