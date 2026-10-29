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
    public async Task Mismatched_gateway_amount_cannot_confirm_deposit()
    {
        await using var s = new TestStore(); var b = await Hold(s); var gateway = new Gateway(); var service = Service(s, gateway); var order = await service.CreateAsync(b.Id, default);
        gateway.Result = new("00", "", order.OrderId, 299, "BANK1", "SUCCESS", null, null);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.InquireAsync(order.OrderId, default));
        Assert.Empty(await s.Db.PhieuCocs.ToListAsync()); Assert.Equal("HOLD", b.TrangThai);
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
