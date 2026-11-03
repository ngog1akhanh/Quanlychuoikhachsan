using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Domain.Entities;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;
public sealed class InventoryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static CreatePurchaseRequest Purchase(long item = 1, decimal quantity = 2, decimal price = 10, decimal discount = 0, decimal tax = 0)
        => new(1, 1, 999, discount, tax, [new(item, quantity, price, null, null, null, null)], null);
    [Fact]
    public async Task Purchase_confirm_records_exact_ledger_lot_and_authenticated_employee_once()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        var p = await s.Inventory.CreatePurchaseAsync(Purchase(), Ct); var d = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        Assert.Equal(1, (await s.Db.PhieuNhaps.SingleAsync()).NhanVienTaoId);
        var r = new ConfirmPurchaseRequest([new(d.Id, "LOT1", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), null)]);
        await s.Inventory.ConfirmPurchaseAsync(p.Id, r, Ct);
        var ledger = await s.Db.GiaoDichKhos.SingleAsync(); var lot = await s.Db.LoHangs.SingleAsync();
        Assert.Equal(2, ledger.SoLuongThayDoi); Assert.Equal(lot.Id, ledger.LoHangId); Assert.Equal(1, ledger.NhanVienId);
        Assert.Equal(d.Id, ledger.ChiTietPhieuNhapId); Assert.Equal(p.Id, ledger.PhieuNhapId);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.ConfirmPurchaseAsync(p.Id, r, Ct));
        Assert.Single(await s.Db.GiaoDichKhos.ToListAsync());
    }
    [Theory]
    [InlineData(-1, 10, 0, 0)] [InlineData(0, 10, 0, 0)] [InlineData(1, -10, 0, 0)] [InlineData(1, 10, -1, 0)] [InlineData(1, 10, 0, -1)] [InlineData(1, 10, 11, 0)] [InlineData(1.0001, 10, 0, 0)]
    public async Task Invalid_purchase_does_not_persist(decimal quantity, decimal price, decimal discount, decimal tax)
    {
        await using var s = new TestStore(); await s.SeedAsync();
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.CreatePurchaseAsync(Purchase(1, quantity, price, discount, tax), Ct));
        Assert.Empty(await s.Db.PhieuNhaps.ToListAsync());
    }
    [Fact]
    public async Task Wrong_detail_and_missing_expiry_are_rejected_before_writes()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var p = await s.Inventory.CreatePurchaseAsync(Purchase(), Ct); var d = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(999, "LOT", null, null, null)]), Ct));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(d.Id, "LOT", null, null, null)]), Ct));
        Assert.Empty(await s.Db.GiaoDichKhos.ToListAsync()); Assert.Empty(await s.Db.LoHangs.ToListAsync());
    }
    [Fact]
    public async Task Expired_receipt_is_rejected()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var p = await s.Inventory.CreatePurchaseAsync(Purchase(), Ct); var d = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(d.Id, "OLD", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null)]), Ct));
        Assert.Empty(await s.Db.LoHangs.ToListAsync());
    }
    [Fact]
    public async Task Individual_assets_require_integral_quantity_and_unique_serials()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.CreatePurchaseAsync(Purchase(2, 1.5m), Ct));
        var p = await s.Inventory.CreatePurchaseAsync(Purchase(2), Ct); var d = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        await Assert.ThrowsAsync<AlreadyExistsException>(() => s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(d.Id, null, null, null, ["S1", "S1"])]), Ct));
        Assert.Empty(await s.Db.TaiSanCaThes.ToListAsync());
        await s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(d.Id, null, null, null, ["S1", "S2"])]), Ct);
        Assert.Equal(2, await s.Db.TaiSanCaThes.CountAsync());
    }
    [Fact]
    public async Task Confirmed_purchase_cannot_be_edited_or_cancelled()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var p = await s.Inventory.CreatePurchaseAsync(Purchase(3), Ct); var d = await s.Db.ChiTietPhieuNhaps.SingleAsync();
        await s.Inventory.ConfirmPurchaseAsync(p.Id, new([new(d.Id, null, null, null, null)]), Ct);
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.CancelDocumentAsync(p.Id, true, "wrong", Ct));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.UpdatePurchaseAsync(p.Id, new(1, 1, 0, 0, Purchase(3).Lines, null), Ct));
    }
    [Fact]
    public async Task Fefo_uses_earliest_nonexpired_lot_and_tracks_repeated_item_lines()
    {
        await using var s = new TestStore(); await s.SeedAsync(); var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var (id, expiry, amount) in new[] { (1L, today.AddDays(-1), 100m), (2L, today.AddDays(1), 3m), (3L, today.AddDays(20), 4m) })
        {
            s.Db.Add(new LoHang { Id = id, MaLo = $"L{id}", KhoId = 1, DanhMucHangHoaId = 1, ChiTietPhieuNhapId = id, HanSuDung = expiry, SoLuongBanDau = amount });
            s.Db.Add(new GiaoDichKho { MaGiaoDich = $"IN{id}", KhoId = 1, DanhMucHangHoaId = 1, LoHangId = id, LoaiGiaoDich = "NHAP", SoLuongThayDoi = amount });
        }
        await s.Db.SaveChangesAsync();
        var issue = await s.Inventory.CreateIssueAsync(new(1, 999, "BEP", [new(1, 2), new(1, 3)], "Kitchen", null), Ct);
        await s.Inventory.ConfirmIssueAsync(issue.Id, Ct);
        var rows = await s.Db.GiaoDichKhos.Where(x => x.LoaiGiaoDich == "XUAT").ToListAsync();
        Assert.DoesNotContain(rows, x => x.LoHangId == 1); Assert.Equal(-3, rows.Where(x => x.LoHangId == 2).Sum(x => x.SoLuongThayDoi)); Assert.Equal(-2, rows.Where(x => x.LoHangId == 3).Sum(x => x.SoLuongThayDoi));
    }
    [Fact]
    public async Task Negative_adjustment_and_mismatched_lot_cannot_corrupt_balances()
    {
        await using var s = new TestStore(); await s.SeedAsync();
        await Assert.ThrowsAsync<AlreadyExistsException>(() => s.Inventory.AdjustAsync(new(1, 3, null, -1, "DIEU_CHINH", "count"), Ct));
        s.Db.Add(new LoHang { Id = 1, MaLo = "L", KhoId = 2, DanhMucHangHoaId = 1, ChiTietPhieuNhapId = 1, SoLuongBanDau = 2 }); await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidArgumentException>(() => s.Inventory.AdjustAsync(new(1, 1, 1, 2, "DIEU_CHINH", "count"), Ct));
        Assert.Empty(await s.Db.GiaoDichKhos.ToListAsync());
    }
}
