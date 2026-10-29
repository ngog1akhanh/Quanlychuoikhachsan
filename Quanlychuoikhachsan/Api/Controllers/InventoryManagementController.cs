using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

[ApiController, Authorize, Route("v1")]
public sealed class InventoryManagementController(HotelDbContext db, InventoryService inventory) : ControllerBase
{
    [HttpGet("suppliers"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Suppliers([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.NhaCungCaps.AsNoTracking()
        .Where(x => (q.Status == null || x.TrangThai == q.Status) && (q.Search == null || x.TenNhaCungCap.Contains(q.Search) || x.MaNhaCungCap.Contains(q.Search)))
        .OrderBy(x => x.Id).PageAsync(q, ct));

    [HttpGet("suppliers/{id:long}"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Supplier(long id, CancellationToken ct) => Ok(await db.NhaCungCaps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Supplier was not found."));

    [HttpPost("suppliers"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> CreateSupplier(SupplierRequest r, CancellationToken ct)
    {
        var row = new NhaCungCap { CreatedAt = DateTime.UtcNow }; Apply(row, r); db.Add(row); await db.SaveChangesAsync(ct);
        return base.Created($"/v1/suppliers/{row.Id}", row);
    }
    [HttpPut("suppliers/{id:long}"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> UpdateSupplier(long id, SupplierRequest r, CancellationToken ct)
    {
        var row = await db.NhaCungCaps.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Supplier was not found.");
        Apply(row, r); await db.SaveChangesAsync(ct); return Ok(row);
    }
    private static void Apply(NhaCungCap x, SupplierRequest r)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        x.MaNhaCungCap = Input.Text(r.Code, 30, "code"); x.TenNhaCungCap = Input.Text(r.Name, 150, "name");
        x.NguoiLienHe = r.Contact; x.SoDienThoai = r.Phone; x.Email = r.Email; x.DiaChi = r.Address; x.MaSoThue = r.TaxCode; x.GhiChu = r.Note; x.TrangThai = r.Status; x.UpdatedAt = DateTime.UtcNow;
    }

    [HttpGet("warehouses"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await db.Khos.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));

    [HttpPost("warehouses"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> CreateWarehouse(WarehouseRequest r, CancellationToken ct)
    {
        var row = new Kho { CreatedAt = DateTime.UtcNow }; Apply(row, r); db.Add(row); await db.SaveChangesAsync(ct); return base.Created($"/v1/warehouses/{row.Id}", row);
    }
    [HttpPut("warehouses/{id:long}"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> UpdateWarehouse(long id, WarehouseRequest r, CancellationToken ct)
    {
        var row = await db.Khos.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Warehouse was not found.");
        Apply(row, r); await db.SaveChangesAsync(ct); return Ok(row);
    }
    private static void Apply(Kho x, WarehouseRequest r)
    {
        Input.OneOf(r.Type, "KHO_CHINH", "KHO_THUC_PHAM", "KHO_MAT", "KHO_DONG", "KHO_DO_UONG", "KHO_BUONG_PHONG", "KHO_TAI_SAN");
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Optional(r.Location, 255, "location");
        if (r.MaximumTemperature < r.MinimumTemperature || r.MinimumTemperature is < -999.99m or > 999.99m || r.MaximumTemperature is < -999.99m or > 999.99m) throw new InvalidArgumentException("Invalid temperature range.");
        x.MaKho = Input.Text(r.Code, 30, "code"); x.TenKho = Input.Text(r.Name, 100, "name"); x.LoaiKho = r.Type; x.ViTri = r.Location; x.NhietDoTu = r.MinimumTemperature; x.NhietDoDen = r.MaximumTemperature; x.TrangThai = r.Status; x.UpdatedAt = DateTime.UtcNow;
    }

    [HttpGet("inventory-units"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Units(CancellationToken ct) => Ok(await db.DonViTinhs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));
    [HttpPost("inventory-units"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> CreateUnit(LookupRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Optional(r.Description, 255, "description");
        var x = new DonViTinh { MaDonVi = Input.Text(r.Code, 30, "code"), TenDonVi = Input.Text(r.Name, 80, "name"), MoTa = r.Description, TrangThai = r.Status };
        db.Add(x); await db.SaveChangesAsync(ct); return base.Created($"/v1/inventory-units/{x.Id}", x);
    }
    [HttpGet("inventory-groups"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Groups(CancellationToken ct) => Ok(await db.NhomHangs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));
    [HttpPut("inventory-units/{id:long}"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> UpdateUnit(long id, LookupRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Optional(r.Description, 255, "description");
        var x = await db.DonViTinhs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Unit was not found.");
        x.MaDonVi = Input.Text(r.Code, 30, "code"); x.TenDonVi = Input.Text(r.Name, 80, "name"); x.MoTa = r.Description; x.TrangThai = r.Status;
        await db.SaveChangesAsync(ct); return Ok(x);
    }
    [HttpPut("inventory-groups/{id:long}"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> UpdateGroup(long id, LookupRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Optional(r.Description, 500, "description");
        var x = await db.NhomHangs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Group was not found.");
        x.MaNhom = Input.Text(r.Code, 30, "code"); x.TenNhom = Input.Text(r.Name, 100, "name"); x.MoTa = r.Description; x.TrangThai = r.Status;
        await db.SaveChangesAsync(ct); return Ok(x);
    }
    [HttpPost("inventory-groups"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> CreateGroup(LookupRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Optional(r.Description, 500, "description");
        var x = new NhomHang { MaNhom = Input.Text(r.Code, 30, "code"), TenNhom = Input.Text(r.Name, 100, "name"), MoTa = r.Description, TrangThai = r.Status };
        db.Add(x); await db.SaveChangesAsync(ct); return base.Created($"/v1/inventory-groups/{x.Id}", x);
    }
    [HttpGet("inventory-items/{id:long}"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Item(long id, CancellationToken ct) => Ok(await db.DanhMucHangHoas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Item was not found."));
    [HttpPatch("inventory-items/{id:long}/status"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> ItemStatus(long id, StatusRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        var x = await db.DanhMucHangHoas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Item was not found.");
        x.TrangThai = r.Status; x.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(x);
    }
    [HttpPut("inventory-items/{id:long}"), RequirePermission("INVENTORY_WRITE")]
    public async Task<IActionResult> UpdateItem(long id, CreateInventoryItemRequest r, CancellationToken ct) => Ok(await inventory.UpdateItemAsync(id, r, ct));
    [HttpGet("purchase-orders"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Purchases([FromQuery] ListQuery q, long? supplierId, long? warehouseId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue) Input.Range(from.Value, to.Value);
        var start = from?.ToDateTime(TimeOnly.MinValue); var end = to?.ToDateTime(TimeOnly.MaxValue);
        return Ok(await db.PhieuNhaps.AsNoTracking().Where(x => (q.Status == null || x.TrangThai == q.Status) && (q.Search == null || x.MaPhieuNhap.Contains(q.Search))
            && (!supplierId.HasValue || x.NhaCungCapId == supplierId) && (!warehouseId.HasValue || x.KhoId == warehouseId) && (!start.HasValue || x.NgayNhap >= start) && (!end.HasValue || x.NgayNhap <= end))
            .OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpGet("purchase-orders/{id:long}"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Purchase(long id, CancellationToken ct) => Ok(await db.PhieuNhaps.AsNoTracking().Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Purchase was not found."));
    [HttpPut("purchase-orders/{id:long}"), RequirePermission("PURCHASE_WRITE")]
    public async Task<IActionResult> UpdatePurchase(long id, UpdatePurchaseRequest r, CancellationToken ct) => Ok(await inventory.UpdatePurchaseAsync(id, r, ct));
    [HttpPost("purchase-orders/{id:long}:cancel"), RequirePermission("PURCHASE_WRITE")]
    public async Task<IActionResult> CancelPurchase(long id, ReasonRequest r, CancellationToken ct) { await inventory.CancelDocumentAsync(id, true, r.Reason, ct); return NoContent(); }
    [HttpGet("stock-issues"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Issues([FromQuery] ListQuery q, long? warehouseId, CancellationToken ct) => Ok(await db.PhieuXuats.AsNoTracking()
        .Where(x => (q.Status == null || x.TrangThai == q.Status) && (!warehouseId.HasValue || x.KhoId == warehouseId)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpGet("stock-issues/{id:long}"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Issue(long id, CancellationToken ct) => Ok(await db.PhieuXuats.AsNoTracking().Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Issue was not found."));
    [HttpPost("stock-issues/{id:long}:cancel"), RequirePermission("STOCK_ISSUE_WRITE")]
    public async Task<IActionResult> CancelIssue(long id, ReasonRequest r, CancellationToken ct) { await inventory.CancelDocumentAsync(id, false, r.Reason, ct); return NoContent(); }
    [HttpPut("stock-issues/{id:long}"), RequirePermission("STOCK_ISSUE_WRITE")]
    public async Task<IActionResult> UpdateIssue(long id, CreateIssueRequest r, CancellationToken ct) => Ok(await inventory.UpdateIssueAsync(id, r, ct));
    [HttpPost("stock-adjustments"), RequirePermission("STOCK_ADJUST")]
    public async Task<IActionResult> Adjust(StockAdjustmentRequest r, CancellationToken ct) => Ok(await inventory.AdjustAsync(r, ct));
    [HttpGet("stock-ledger"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Ledger([FromQuery] ListQuery q, long? warehouseId, long? itemId, long? lotId, CancellationToken ct) => Ok(await db.GiaoDichKhos.AsNoTracking()
        .Where(x => (!warehouseId.HasValue || x.KhoId == warehouseId) && (!itemId.HasValue || x.DanhMucHangHoaId == itemId) && (!lotId.HasValue || x.LoHangId == lotId)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpGet("inventory-lots"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Lots([FromQuery] ListQuery q, long? warehouseId, long? itemId, DateOnly? expiresBefore, CancellationToken ct) => Ok(await db.LoHangs.AsNoTracking()
        .Where(x => (!warehouseId.HasValue || x.KhoId == warehouseId) && (!itemId.HasValue || x.DanhMucHangHoaId == itemId) && (!expiresBefore.HasValue || x.HanSuDung <= expiresBefore))
        .OrderBy(x => x.HanSuDung).ThenBy(x => x.Id).Select(x => new { x.Id, x.MaLo, x.KhoId, x.DanhMucHangHoaId, x.HanSuDung, x.NgaySanXuat, x.TrangThai,
            Balance = db.GiaoDichKhos.Where(g => g.LoHangId == x.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0 }).PageAsync(q, ct));
    [HttpGet("inventory-alerts"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Alerts(long warehouseId, CancellationToken ct)
    {
        if (!await db.Khos.AnyAsync(x => x.Id == warehouseId, ct)) throw new ResourceNotFoundException("Warehouse was not found.");
        return Ok(await db.DanhMucHangHoas.Where(x => x.TrangThai == "HOAT_DONG").Select(x => new { x.Id, x.TenHang, Minimum = x.MucTonToiThieu,
            Balance = db.GiaoDichKhos.Where(g => g.KhoId == warehouseId && g.DanhMucHangHoaId == x.Id).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0 }).Where(x => x.Balance < x.Minimum).ToListAsync(ct));
    }
}
