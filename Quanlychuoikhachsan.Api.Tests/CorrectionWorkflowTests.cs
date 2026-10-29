using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Controllers;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Application.Services;
using Quanlychuoikhachsan.Domain.Entities;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class CorrectionWorkflowTests
{
    [Fact]
    public async Task Voided_service_invoice_can_be_refunded_once_without_deleting_its_payment()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync(); s.UseAccount(2);
        var service = new ServiceOperations(s.Db, s.Actor);
        var request = await service.RequestAsync(new(1, 1, 1, 1, null), default); s.UseAccount(1);
        await service.TransitionAsync(request.Id, "DANG_XU_LY", default); await service.TransitionAsync(request.Id, "HOAN_THANH", default);
        var invoice = await s.Db.Set<HoaDonDichVu>().SingleAsync();
        var payment = await service.PayAsync(invoice.Id, new(1, 125, "SALE1"), default);
        var billing = new BillingManagementController(s.Db, s.Actor, s.Bookings, new NotificationWriter(s.Db));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => billing.CreateRefund(new(null, null, payment.Id, 1, 125, "Service complaint"), default));
        await service.VoidAsync(invoice.Id, "Service complaint", default);
        await billing.CreateRefund(new(null, null, payment.Id, 1, 125, "Service complaint"), default);
        var refund = await s.Db.Set<PhieuHoanTien>().SingleAsync();
        await billing.ConfirmRefund(refund.Id, new("REFUND1"), default); await billing.ConfirmRefund(refund.Id, new("REFUND1"), default);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => billing.CreateRefund(new(null, null, payment.Id, 1, 1, "Duplicate"), default));
        Assert.Single(await s.Db.Set<ThanhToanDichVu>().ToListAsync());
        Assert.Equal("THANH_CONG", refund.TrangThai); Assert.Equal("DA_HUY", invoice.TrangThai);
        Assert.Contains(await s.Db.NhatKyHeThongs.ToListAsync(), x => x.HanhDong == "VOID" && x.DuLieuMoi!.Contains("Service complaint"));
    }

    [Fact]
    public async Task Pending_QR_refund_can_record_an_actual_manual_bank_refund()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync("HET_HAN");
        s.Db.Add(new PhuongThucThanhToan { Id = 2, MaPhuongThuc = "VIETQR", TenPhuongThuc = "QR" });
        s.Db.Add(new PhuongThucThanhToan { Id = 3, MaPhuongThuc = "CHUYEN_KHOAN", TenPhuongThuc = "Transfer" });
        s.Db.Add(new PhieuCoc { Id = 1, MaPhieuCoc = "C1", DatPhongId = 1, PhuongThucId = 2, SoTien = 30, TrangThai = "DA_THANH_TOAN" });
        s.Db.Add(new PhieuHoanTien { Id = 1, MaPhieuHoan = "R1", PhieuCocId = 1, PhuongThucId = 2, SoTien = 30, LyDo = "Expired hold", TrangThai = "CHO_XU_LY" }); await s.Db.SaveChangesAsync();
        var billing = new BillingManagementController(s.Db, s.Actor, s.Bookings, new NotificationWriter(s.Db));
        await billing.ConfirmRefund(1, new("BANK-REFUND", 3), default);
        var refund = await s.Db.Set<PhieuHoanTien>().SingleAsync(); Assert.Equal(3, refund.PhuongThucId); Assert.Equal("THANH_CONG", refund.TrangThai);
        Assert.Equal(30, (await s.Db.PhieuCocs.SingleAsync()).SoTien);
    }

    [Fact]
    public async Task Asset_transfer_and_repair_keep_the_warehouse_ledger_balanced()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        var purchase = await s.Inventory.CreatePurchaseAsync(new(1, 1, 999, 0, 0, [new(2, 1, 50, null, null, null, null)], null), default);
        var line = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        await s.Inventory.ConfirmPurchaseAsync(purchase.Id, new([new(line.Id, null, null, null, [" CHAIR-1 "])]), default);
        var asset = await s.Db.TaiSanCaThes.SingleAsync(); Assert.Equal("CHAIR-1", asset.SoSerial);
        var controller = new AssetsController(s.Db, s.Actor);
        await controller.Move(asset.Id, new(null, 2, null, "Transfer warehouse"), default);
        Assert.Equal(0, (await s.Inventory.GetBalanceAsync(1, 2, default)).Balance);
        Assert.Equal(1, (await s.Inventory.GetBalanceAsync(2, 2, default)).Balance);
        await controller.Condition(asset.Id, new("HONG", "Broken"), default);
        Assert.Equal(0, (await s.Inventory.GetBalanceAsync(2, 2, default)).Balance);
        await controller.Condition(asset.Id, new("TOT", "Repaired"), default);
        Assert.Equal(1, (await s.Inventory.GetBalanceAsync(2, 2, default)).Balance);
    }

    [Fact]
    public async Task Issued_documents_prevent_changing_item_tracking_or_editing_issue_lines()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        await s.Inventory.AdjustAsync(new(1, 3, null, 10, "DIEU_CHINH", "Opening count"), default);
        var issue = await s.Inventory.CreateIssueAsync(new(1, 999, "BUONG_PHONG", [new(3, 2)], null, null), default);
        await s.Inventory.UpdateIssueAsync(issue.Id, new(1, 999, "BUONG_PHONG", [new(3, 4)], null, null), default);
        await s.Inventory.ConfirmIssueAsync(issue.Id, default);
        Assert.Equal(6, (await s.Inventory.GetBalanceAsync(1, 3, default)).Balance);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.UpdateIssueAsync(issue.Id, new(1, 999, "BUONG_PHONG", [new(3, 1)], null, null), default));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.UpdateItemAsync(3, new("SOAP", "Soap", "VAT_TU", 1, null, 1, 1, true, false, false, null, null), default));
    }

    [Fact]
    public async Task Invoice_replacement_keeps_the_voided_invoice_and_links_to_its_successor()
    {
        await using var s = new TestStore(); await s.SeedAsync(); await s.SeedStayAsync("BOOKED");
        await s.Bookings.CheckInAsync(1, new(1), default);
        var first = await s.Bookings.PrepareInvoiceAsync(1, new(1, 0, 0), default);
        var billing = new BillingManagementController(s.Db, s.Actor, s.Bookings, new NotificationWriter(s.Db));
        await billing.Void(first.Id, new("Correct surcharge"), default);
        var second = await s.Bookings.PrepareInvoiceAsync(1, new(1, 10, 0), default);
        Assert.Equal(110, second.Total); Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, (await s.Db.HoaDonPhongs.SingleAsync(x => x.Id == first.Id)).HoaDonThayTheId);
    }
}
