using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Domain.Entities;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;
public sealed class DepositAndMaintenanceTests
{
    private sealed class Gateway : IVietQrGateway
    {
        public VietQrCreateCustomerQrRequest? Created { get; private set; }
        public VietQrInquiryResult? Result { get; set; }
        public Task<VietQrCustomerQrResult> CreateCustomerQrAsync(VietQrCreateCustomerQrRequest request, CancellationToken ct) { Created = request; return Task.FromResult(new VietQrCustomerQrResult("qr", "data")); }
        public Task<VietQrInquiryResult> InquireAsync(string id, CancellationToken ct) => Task.FromResult(Result!);
    }
    private static DepositPaymentService Service(TestStore s, Gateway gateway) => new(s.Db, s.Actor, s.Bookings, gateway, Options.Create(new VietQrSettings()), new NotificationWriter(s.Db));
    private static async Task<DatPhong> Hold(TestStore s)
    {
        await s.SeedAsync(); var booking = await s.SeedStayAsync("HOLD"); booking.TongTienDuKien = 1000; booking.DuKienNhan = DateTime.UtcNow.AddDays(1); booking.DuKienTra = DateTime.UtcNow.AddDays(2);
        s.Db.Add(new KhoaLichPhong { ChiTietDatPhongId = 1, PhongId = 1, NgayLuuTru = DateOnly.FromDateTime(booking.DuKienNhan), TrangThai = "HOLD", HetHanGiuCho = DateTime.UtcNow.AddMinutes(10) });
        s.Db.Add(new PhuongThucThanhToan { Id = 2, MaPhuongThuc = "VIETQR", TenPhuongThuc = "VietQR" }); await s.Db.SaveChangesAsync(); s.UseAccount(2); return booking;
    }
    [Fact]
    public async Task Deposit_amount_is_server_calculated_and_verified_inquiry_is_idempotent()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway);
        var order = await service.CreateAsync(b.Id, default); Assert.Equal(300, gateway.Created!.Amount);
        gateway.Result = new("00", "", order.OrderId, 300, "BANK1", "SUCCESS", null, null);
        await service.InquireAsync(order.OrderId, default); await service.InquireAsync(order.OrderId, default);
        Assert.Equal("BOOKED", b.TrangThai); Assert.Single(await s.Db.PhieuCocs.ToListAsync()); Assert.Equal("BOOKED", (await s.Db.KhoaLichPhongs.SingleAsync()).TrangThai);
    }
    [Fact]
    public async Task Underpaid_deposit_is_recorded_for_refund_without_confirming_booking()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        gateway.Result = new("00", "", order.OrderId, 299, "BANK1", "SUCCESS", null, null);
        await service.InquireAsync(order.OrderId, default);
        Assert.Equal("REFUND_REQUIRED", order.Status); Assert.Equal("HOLD", b.TrangThai);
        Assert.Equal(299, (await s.Db.PhieuCocs.SingleAsync()).SoTien); Assert.Equal(299, (await s.Db.Set<PhieuHoanTien>().SingleAsync()).SoTien);
    }
    [Fact]
    public async Task Mismatched_gateway_order_cannot_confirm_deposit()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        gateway.Result = new("00", "", "another-order", 300, "BANK1", "SUCCESS", null, null);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.InquireAsync(order.OrderId, default));
        Assert.Empty(await s.Db.PhieuCocs.ToListAsync()); Assert.Equal("HOLD", b.TrangThai);
    }
    [Fact]
    public async Task Creating_qr_extends_hold_and_failed_qr_can_be_renewed()
    {
        await using var s = new TestStore(); var b = await Hold(s); var hold = await s.Db.KhoaLichPhongs.SingleAsync();
        hold.HetHanGiuCho = DateTime.UtcNow.AddMinutes(2); await s.Db.SaveChangesAsync();
        var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        Assert.True(hold.HetHanGiuCho >= DateTime.UtcNow.AddMinutes(9)); Assert.True(order.Description.Length <= 25);
        Assert.Same(order, await service.CreateAsync(b.Id, default));
        var firstId = order.OrderId; gateway.Result = new("00", "", firstId, 0, null, "EXPIRED", null, null);
        await service.InquireAsync(firstId, default); Assert.Equal("FAILED", order.Status);
        var renewed = await service.CreateAsync(b.Id, default);
        Assert.Equal("PENDING", renewed.Status); Assert.NotEqual(firstId, renewed.OrderId); Assert.Single(await s.Db.Set<DepositPaymentOrder>().ToListAsync());
    }
    [Fact]
    public async Task Demo_mode_can_simulate_paid_deposit()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        await service.SimulatePaidAsync(order.OrderId, default);
        Assert.Equal("PAID", order.Status); Assert.Equal("BOOKED", b.TrangThai); Assert.StartsWith("DEMO", (await s.Db.PhieuCocs.SingleAsync()).MaGiaoDich);
        var live = new DepositPaymentService(s.Db, s.Actor, s.Bookings, gateway, Options.Create(new VietQrSettings { Mode = "Live" }), new NotificationWriter(s.Db));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => live.SimulatePaidAsync(order.OrderId, default));
    }
    [Fact]
    public void Demo_qr_payload_follows_vietqr_format()
    {
        var payload = Quanlychuoikhachsan.Infrastructure.Payments.DemoVietQrGateway.BuildPayload("970436", "0123456789", 300000, "COC12 ABCDEF12");
        Assert.StartsWith("000201010212", payload); Assert.Contains("0010A000000727", payload); Assert.Contains("5406300000", payload);
        Assert.Contains("62180814COC12 ABCDEF12", payload); Assert.Matches("6304[0-9A-F]{4}$", payload);
    }
    [Fact]
    public async Task Late_payment_records_money_and_refund_without_claiming_an_expired_room()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        (await s.Db.KhoaLichPhongs.SingleAsync()).HetHanGiuCho = DateTime.UtcNow.AddMinutes(-1); await s.Db.SaveChangesAsync();
        await new BookingMaintenance(s.Db, new NotificationWriter(s.Db)).RunAsync(default); Assert.Equal("HET_HAN", b.TrangThai); Assert.Empty(await s.Db.KhoaLichPhongs.ToListAsync());
        gateway.Result = new("00", "", order.OrderId, 300, "BANK1", "SUCCESS", null, null); await service.InquireAsync(order.OrderId, default);
        Assert.Equal("HET_HAN", b.TrangThai); Assert.Equal("REFUND_REQUIRED", order.Status); Assert.Single(await s.Db.Set<PhieuHoanTien>().ToListAsync()); Assert.Single(await s.Db.PhieuCocs.ToListAsync());
    }
    [Fact]
    public async Task Reminder_is_created_once_for_each_stay_schedule()
    {
        await using var s = new TestStore(); var b = await Hold(s); b.TrangThai = "BOOKED"; b.DuKienNhan = DateTime.UtcNow.AddHours(5); await s.Db.SaveChangesAsync();
        var maintenance = new BookingMaintenance(s.Db, new NotificationWriter(s.Db)); await maintenance.RunAsync(default); await maintenance.RunAsync(default);
        Assert.Single(await s.Db.Set<ThongBao>().Where(x => x.LoaiThongBao == "BOOKING_ARRIVAL_REMINDER").ToListAsync());
    }
}
