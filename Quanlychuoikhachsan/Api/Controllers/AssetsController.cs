using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

[ApiController, Authorize, Route("v1/assets")]
public sealed class AssetsController(HotelDbContext db, CurrentActor actor) : ControllerBase
{
    [HttpGet, RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> List([FromQuery] ListQuery q, long? roomId, long? warehouseId, CancellationToken ct) => Ok(await db.TaiSanCaThes.AsNoTracking()
        .Where(x => (!roomId.HasValue || x.PhongId == roomId) && (!warehouseId.HasValue || x.KhoId == warehouseId) && (q.Status == null || x.TinhTrang == q.Status) && (q.Search == null || x.MaTaiSan.Contains(q.Search) || (x.SoSerial != null && x.SoSerial.Contains(q.Search)))).OrderBy(x => x.Id).PageAsync(q, ct));
    [HttpGet("{id:long}"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await db.TaiSanCaThes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Asset was not found."));
    private async Task<TaiSanCaThe> Lock(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational()) await db.TaiSanCaThes.FromSqlInterpolated($"SELECT * FROM tai_san_ca_the WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        return await db.TaiSanCaThes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Asset was not found.");
    }
    private async Task WarehouseLock(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational()) await db.Khos.FromSqlInterpolated($"SELECT * FROM kho WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        if (!await db.Khos.AnyAsync(x => x.Id == id && x.TrangThai == "HOAT_DONG", ct)) throw new ResourceNotFoundException("Active warehouse was not found.");
    }
    private async Task PostStock(TaiSanCaThe asset, long warehouse, decimal quantity, string type, string reason, CancellationToken ct)
    {
        var item = await db.DanhMucHangHoas.SingleAsync(x => x.Id == asset.DanhMucHangHoaId, ct); long? lotId = null;
        if (item.TheoDoiLo)
        {
            var lot = await db.LoHangs.SingleOrDefaultAsync(x => x.KhoId == warehouse && x.ChiTietPhieuNhapId == asset.ChiTietPhieuNhapId && x.DanhMucHangHoaId == item.Id, ct);
            if (lot is null && quantity > 0)
            {
                var source = await db.LoHangs.FirstOrDefaultAsync(x => x.ChiTietPhieuNhapId == asset.ChiTietPhieuNhapId && x.DanhMucHangHoaId == item.Id, ct) ?? throw new InvalidArgumentException("Asset receiving lot was not found.");
                lot = new LoHang { MaLo = $"TRANSFER-{source.ChiTietPhieuNhapId}-{warehouse}", KhoId = warehouse, DanhMucHangHoaId = item.Id, ChiTietPhieuNhapId = source.ChiTietPhieuNhapId, NgaySanXuat = source.NgaySanXuat, HanSuDung = source.HanSuDung, SoLuongBanDau = 1, DonGiaNhap = source.DonGiaNhap, CreatedAt = DateTime.UtcNow };
                db.Add(lot); await db.SaveChangesAsync(ct);
            }
            lotId = lot?.Id ?? throw new InvalidArgumentException("Asset lot was not found in this warehouse.");
        }
        if (quantity < 0)
        {
            var balance = await db.GiaoDichKhos.Where(x => x.KhoId == warehouse && x.DanhMucHangHoaId == item.Id && (!lotId.HasValue || x.LoHangId == lotId)).SumAsync(x => (decimal?)x.SoLuongThayDoi, ct) ?? 0;
            if (balance < -quantity) throw new AlreadyExistsException("Asset ledger has insufficient stock.");
        }
        db.Add(new GiaoDichKho { MaGiaoDich = $"TS{Guid.NewGuid():N}", KhoId = warehouse, DanhMucHangHoaId = item.Id, LoHangId = lotId, NhanVienId = await actor.EmployeeIdAsync(ct), LoaiGiaoDich = type, SoLuongThayDoi = quantity, ThoiGian = DateTime.UtcNow, ThamChieu = $"assets/{asset.Id}", GhiChu = reason });
    }
    [HttpPost("{id:long}:transfer"), RequirePermission("ASSET_WRITE")]
    public async Task<IActionResult> Move(long id, AssetMoveRequest r, CancellationToken ct)
    {
        Input.Text(r.Reason, 500, "reason"); Input.Optional(r.OtherLocation, 255, "other_location");
        if ((r.RoomId.HasValue ? 1 : 0) + (r.WarehouseId.HasValue ? 1 : 0) + (!string.IsNullOrWhiteSpace(r.OtherLocation) ? 1 : 0) != 1) throw new InvalidArgumentException("Specify exactly one destination.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var asset = await Lock(id, ct);
        if (asset.TinhTrang != "TOT") throw new InvalidArgumentException("Only usable assets can be transferred.");
        if (r.RoomId.HasValue && !await db.Phongs.AnyAsync(x => x.Id == r.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        if (asset.KhoId == r.WarehouseId && asset.PhongId == r.RoomId && asset.ViTriKhac == r.OtherLocation) throw new InvalidArgumentException("Asset is already at this location.");
        foreach (var warehouse in new[] { asset.KhoId, r.WarehouseId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order()) await WarehouseLock(warehouse, ct);
        if (asset.KhoId.HasValue) await PostStock(asset, asset.KhoId.Value, -1, "XUAT", r.Reason, ct);
        if (r.WarehouseId.HasValue) await PostStock(asset, r.WarehouseId.Value, 1, "NHAP", r.Reason, ct);
        asset.KhoId = r.WarehouseId; asset.PhongId = r.RoomId; asset.ViTriKhac = r.OtherLocation; asset.TrangThaiSuDung = r.WarehouseId.HasValue ? "TRONG_KHO" : "DANG_SU_DUNG"; asset.GhiChu = r.Reason; asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(asset);
    }
    [HttpPost("{id:long}:condition"), RequirePermission("ASSET_WRITE")]
    public async Task<IActionResult> Condition(long id, AssetConditionRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Condition, "TOT", "HONG", "MAT", "BAO_TRI", "THANH_LY"); Input.Text(r.Reason, 500, "reason");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var asset = await Lock(id, ct);
        if (asset.TinhTrang is "MAT" or "THANH_LY" || asset.TinhTrang == r.Condition) throw new InvalidArgumentException("Asset condition cannot transition.");
        var wasUsable = asset.TinhTrang is "TOT" or "BAO_TRI"; var isUsable = r.Condition is "TOT" or "BAO_TRI";
        if (asset.KhoId.HasValue && wasUsable != isUsable)
        {
            await WarehouseLock(asset.KhoId.Value, ct); await PostStock(asset, asset.KhoId.Value, isUsable ? 1 : -1, isUsable ? "DIEU_CHINH" : r.Condition == "THANH_LY" ? "THANH_LY" : "HONG_MAT", r.Reason, ct);
        }
        asset.TinhTrang = r.Condition; asset.TrangThaiSuDung = r.Condition switch { "MAT" => "MAT", "THANH_LY" => "THANH_LY", "BAO_TRI" or "HONG" => "BAO_TRI", _ => asset.KhoId.HasValue ? "TRONG_KHO" : "DANG_SU_DUNG" };
        asset.GhiChu = r.Reason; asset.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(asset);
    }
}
