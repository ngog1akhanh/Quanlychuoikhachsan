using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Controllers;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Services;
using Quanlychuoikhachsan.Domain.Entities;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;
public sealed class WorkflowTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    [Fact]
    public async Task Customer_cannot_read_another_customers_booking_or_request_service()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync(); s.UseAccount(3);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => s.Actor.BookingAsync(1, "BOOKING_READ", Ct));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => new ServiceOperations(s.Db, s.Actor).RequestAsync(new(1, 1, 1, 1, null), Ct));
    }
    [Fact]
    public async Task Service_snapshots_price_and_creates_separate_invoice_only_once()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync(); s.UseAccount(2);
        var ops = new ServiceOperations(s.Db, s.Actor); var row = await ops.RequestAsync(new(1, 1, 1, 2, null), Ct);
        (await s.Db.Set<DichVu>().SingleAsync()).DonGia = 999; await s.Db.SaveChangesAsync(); s.UseAccount(1);
        await ops.TransitionAsync(row.Id, "DANG_XU_LY", Ct); await ops.TransitionAsync(row.Id, "HOAN_THANH", Ct);
        var invoice = await s.Db.Set<HoaDonDichVu>().SingleAsync(); Assert.Equal(250, invoice.TongTien);
        Assert.Empty(await s.Db.HoaDonPhongs.ToListAsync()); Assert.Single(await s.Db.Set<ChiTietHoaDonDichVu>().ToListAsync());
        await Assert.ThrowsAsync<InvalidArgumentException>(() => ops.TransitionAsync(row.Id, "HOAN_THANH", Ct));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => ops.PayAsync(invoice.Id, new(1, 251, "over"), Ct));
        var payment = await ops.PayAsync(invoice.Id, new(1, 250, "paid"), Ct);
        Assert.Equal(payment.Id, (await ops.PayAsync(invoice.Id, new(1, 250, "paid"), Ct)).Id);
        Assert.Single(await s.Db.Set<ThanhToanDichVu>().ToListAsync());
    }
    [Fact]
    public async Task Review_requires_completed_stay_and_one_review_per_booking()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var b = await s.SeedStayAsync(); s.UseAccount(2);
        var controller = new ReviewsController(s.Db, s.Actor); var r = new ReviewRequest(1, 5, 4, 5, "Good");
        await Assert.ThrowsAsync<InvalidArgumentException>(() => controller.Create(r, Ct)); b.TrangThai = "DA_TRA"; await s.Db.SaveChangesAsync();
        Assert.IsType<CreatedResult>(await controller.Create(r, Ct));
        await Assert.ThrowsAsync<AlreadyExistsException>(() => controller.Create(r, Ct));
        Assert.Single(await s.Db.Set<DanhGia>().ToListAsync());
    }
    [Fact]
    public async Task Completing_daily_cleaning_keeps_occupied_room_occupied()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync(); var room = await s.Db.Phongs.SingleAsync(x => x.Id == 1); room.TrangThai = "DANG_O";
        s.Db.Add(new CongViecBuongPhong { Id = 1, MaCongViec = "HK1", PhongId = 1, DatPhongId = 1, LoaiCongViec = "DON_HANG_NGAY", TrangThai = "DANG_THUC_HIEN", NhanVienDuocGiaoId = 1 }); await s.Db.SaveChangesAsync();
        await new HousekeepingOperationsController(s.Db, s.Actor).Status(1, new("HOAN_THANH"), Ct); Assert.Equal("DANG_O", room.TrangThai);
    }
    [Fact]
    public async Task Checkout_reuses_invoice_and_creates_cleaning_task_without_duplicate_billing()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync("BOOKED");
        var draft = await s.Bookings.CheckInAsync(1, new(999), Ct); Assert.Equal("NHAP", draft.Status);
        var issued = await s.Bookings.PrepareInvoiceAsync(1, new(999, 0, 0), Ct); Assert.Equal(draft.Id, issued.Id); Assert.Equal(100, issued.Due);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Bookings.CheckOutAsync(1, new(999), Ct));
        var paid = await s.Bookings.AddInvoicePaymentAsync(issued.Id, new(1, 100, 999, "MANUAL", "receipt-1"), Ct); Assert.Equal(0, paid.Due);
        await s.Bookings.CheckOutAsync(1, new(999), Ct);
        Assert.Single(await s.Db.HoaDonPhongs.ToListAsync()); Assert.Single(await s.Db.CongViecBuongPhongs.ToListAsync());
        Assert.Equal("CAN_DON", (await s.Db.Phongs.SingleAsync(x => x.Id == 1)).TrangThai);
        Assert.Equal(1, (await s.Db.ThanhToanHoaDonPhongs.SingleAsync()).NhanVienThuId);
    }
    [Fact]
    public async Task Notification_has_immutable_stay_snapshot_and_other_account_cannot_mark_read()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var b = await s.SeedStayAsync(); var original = b.DuKienTra;
        var writer = new NotificationWriter(s.Db); await writer.BookingAsync(b, 1, "BOOKED", "Arrived", Ct);
        b.DuKienTra = b.DuKienTra.AddDays(1); await s.Db.SaveChangesAsync();
        var notification = await s.Db.Set<ThongBao>().SingleAsync(); Assert.Equal(original, notification.CheckOutLuc);
        s.UseAccount(3); await Assert.ThrowsAsync<ResourceNotFoundException>(() => new NotificationsController(s.Db, s.Actor, writer).MarkRead(notification.Id, Ct));
    }
    [Fact]
    public async Task Draft_cancel_preserves_financial_source_and_requires_manager_approval()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var b = await s.SeedStayAsync("BOOKED"); s.UseAccount(2);
        var r = await s.Bookings.CancelAsync(2, b.Id, new("Plans changed"), Ct); Assert.Equal("CHO_DUYET_HUY", b.TrangThai);
        Assert.Equal("CHO_DUYET", r.Status); await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Bookings.CancelAsync(2, b.Id, new("again"), Ct));
    }
    [Fact]
    public async Task Policy_for_other_room_type_does_not_shadow_applicable_price()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)); var day = (byte)(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);
        s.Db.Add(new ChinhSachGia { MaChinhSach = "LOW", TenChinhSach = "Applicable", TuNgay = date, DenNgay = date, MucUuTien = 1, CacThuApDung = [new() { Thu = day }], ChiTiet = [new() { LoaiPhongId = 1, KieuDieuChinh = "GIA_CO_DINH", GiaTri = 200 }] });
        s.Db.Add(new ChinhSachGia { MaChinhSach = "HIGH", TenChinhSach = "Other room type", TuNgay = date, DenNgay = date, MucUuTien = 10, CacThuApDung = [new() { Thu = day }], ChiTiet = [new() { LoaiPhongId = 2, KieuDieuChinh = "GIA_CO_DINH", GiaTri = 999 }] }); await s.Db.SaveChangesAsync();
        var quote = await s.Bookings.PreviewPriceAsync(1, date, date.AddDays(1), Ct); Assert.Equal(200, quote.Total);
    }
}
