using System.Data;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemResponse>> ListItemsAsync(CancellationToken cancellationToken);
    Task<InventoryItemResponse> CreateItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken);
    Task<PurchaseResponse> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken cancellationToken);
    Task<PurchaseResponse> ConfirmPurchaseAsync(long id, ConfirmPurchaseRequest request, CancellationToken cancellationToken);
    Task<IssueResponse> CreateIssueAsync(CreateIssueRequest request, CancellationToken cancellationToken);
    Task<IssueResponse> ConfirmIssueAsync(long id, CancellationToken cancellationToken);
    Task<StockBalanceResponse> GetBalanceAsync(long warehouseId, long itemId, CancellationToken cancellationToken);
}

/// <summary>Qu?n lý m?t hàng và ghi bi?n ??ng nh?p xu?t ?? tính t?n kho.</summary>
public sealed class InventoryService(HotelDbContext db) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryItemResponse>> ListItemsAsync(CancellationToken ct)
    {
        var balances = db.GiaoDichKhos.GroupBy(x => new { x.KhoId, x.DanhMucHangHoaId }).Select(x => new { x.Key.KhoId, x.Key.DanhMucHangHoaId, Balance = x.Sum(v => v.SoLuongThayDoi) });
        return await db.DanhMucHangHoas.AsNoTracking().Select(x => new InventoryItemResponse(x.Id, x.MaHang, x.TenHang, x.LoaiHang, x.DonViTinhId, x.GiaVonThamChieu, x.TheoDoiLo, x.TheoDoiHanSuDung, x.TheoDoiCaThe, balances.Where(b => b.DanhMucHangHoaId == x.Id).Sum(b => (decimal?)b.Balance) ?? 0)).ToListAsync(ct);
    }

    public async Task<InventoryItemResponse> CreateItemAsync(CreateInventoryItemRequest r, CancellationToken ct)
    {
        if (r.ReferenceCost < 0 || r.MinimumStock < 0 || (r.TrackExpiry && !r.TrackLot) || (r.TrackIndividual && r.Type != "TAI_SAN")) throw new InvalidArgumentException("Inventory item tracking flags violate K1 constraints.");
        if (await db.DanhMucHangHoas.AnyAsync(x => x.MaHang == r.Code, ct)) throw new AlreadyExistsException("The inventory item code already exists.");
        var item = new DanhMucHangHoa { MaHang = r.Code, TenHang = r.Name, LoaiHang = r.Type, DonViTinhId = r.UnitId, NhomHangId = r.GroupId, GiaVonThamChieu = r.ReferenceCost, MucTonToiThieu = r.MinimumStock, TheoDoiLo = r.TrackLot, TheoDoiHanSuDung = r.TrackExpiry, TheoDoiCaThe = r.TrackIndividual, DieuKienBaoQuan = r.StorageCondition, MoTa = r.Description, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.DanhMucHangHoas.Add(item); await db.SaveChangesAsync(ct);
        return new(item.Id, item.MaHang, item.TenHang, item.LoaiHang, item.DonViTinhId, item.GiaVonThamChieu, item.TheoDoiLo, item.TheoDoiHanSuDung, item.TheoDoiCaThe, 0);
    }

    public async Task<PurchaseResponse> CreatePurchaseAsync(CreatePurchaseRequest r, CancellationToken ct)
    {
        if (r.Lines is null || r.Lines.Count == 0) throw new InvalidArgumentException("A purchase order cannot be empty.");
        var lines = r.Lines.Select(x => new ChiTietPhieuNhap { DanhMucHangHoaId = x.ItemId, SoLuong = x.Quantity, DonGiaNhap = x.UnitPrice, ThanhTien = decimal.Round(x.Quantity * x.UnitPrice, 2) }).ToList();
        if (lines.Any(x => x.SoLuong <= 0 || x.DonGiaNhap < 0)) throw new InvalidArgumentException("Purchase quantities must be positive and prices cannot be negative.");
        var order = new PhieuNhap { MaPhieuNhap = $"PN{Guid.NewGuid():N}"[..40], NhaCungCapId = r.SupplierId, KhoId = r.WarehouseId, NhanVienTaoId = r.EmployeeId, NgayNhap = DateTime.UtcNow, TongTienHang = lines.Sum(x => x.ThanhTien), ChietKhau = r.Discount, TienThue = r.Tax, TongThanhToan = lines.Sum(x => x.ThanhTien) - r.Discount + r.Tax, GhiChu = r.Note, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ChiTiet = lines };
        db.PhieuNhaps.Add(order); await db.SaveChangesAsync(ct); return new(order.Id, $"purchase-orders/{order.Id}", order.TrangThai, order.TongThanhToan);
    }

    // Xác nh?n nh?p trong transaction, t?o lô và bút toán t?ng t?n khi c?n.
    public async Task<PurchaseResponse> ConfirmPurchaseAsync(long id, ConfirmPurchaseRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.PhieuNhaps.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException($"Purchase order {id} was not found.");
        if (order.TrangThai != "NHAP") throw new InvalidArgumentException("Only a draft purchase order can be confirmed.");
        if (order.ChiTiet.Count == 0) throw new InvalidArgumentException("A purchase order cannot be empty.");
        var expected = order.ChiTiet.Sum(x => x.ThanhTien);
        if (expected != order.TongTienHang || order.TongThanhToan != expected - order.ChietKhau + order.TienThue) throw new InvalidArgumentException("Purchase totals do not match purchase details.");
        if (request.Lines is null || request.Lines.Count != order.ChiTiet.Count || request.Lines.Select(x => x.DetailId).Distinct().Count() != order.ChiTiet.Count) throw new InvalidArgumentException("Every purchase detail must include one receiving allocation.");
        var confirmations = request.Lines.ToDictionary(x => x.DetailId);
        var itemIds = order.ChiTiet.Select(x => x.DanhMucHangHoaId).ToArray();
        var items = await db.DanhMucHangHoas.Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var line in order.ChiTiet)
        {
            var item = items[line.DanhMucHangHoaId];
            var allocation = confirmations[line.Id];
            if (item.TheoDoiHanSuDung && (string.IsNullOrWhiteSpace(allocation.LotCode) || allocation.ExpiryDate is null)) throw new InvalidArgumentException("Expiry-tracked purchase lines require a lot with expiry date.");
            long? lotId = null;
            if (item.TheoDoiLo)
            {
                var lot = new LoHang { MaLo = allocation.LotCode ?? $"AUTO-{line.Id}", DanhMucHangHoaId = item.Id, ChiTietPhieuNhapId = line.Id, KhoId = order.KhoId, NgaySanXuat = allocation.ManufactureDate, HanSuDung = allocation.ExpiryDate, SoLuongBanDau = line.SoLuong, DonGiaNhap = line.DonGiaNhap, CreatedAt = DateTime.UtcNow };
                db.LoHangs.Add(lot); await db.SaveChangesAsync(ct); lotId = lot.Id;
            }
            db.GiaoDichKhos.Add(new GiaoDichKho { MaGiaoDich = $"NK{Guid.NewGuid():N}"[..50], KhoId = order.KhoId, DanhMucHangHoaId = line.DanhMucHangHoaId, LoHangId = lotId, PhieuNhapId = order.Id, ChiTietPhieuNhapId = line.Id, NhanVienId = order.NhanVienXacNhanId, LoaiGiaoDich = "NHAP", SoLuongThayDoi = line.SoLuong, ThoiGian = DateTime.UtcNow });
            if (item.TheoDoiCaThe)
            {
                if (allocation.Serials is not null && allocation.Serials.Count != (int)line.SoLuong) throw new InvalidArgumentException("Asset serial count must equal the received quantity.");
                for (var i = 0; i < line.SoLuong; i++) db.TaiSanCaThes.Add(new TaiSanCaThe { MaTaiSan = $"TS{Guid.NewGuid():N}"[..40], SoSerial = allocation.Serials?[i], DanhMucHangHoaId = item.Id, ChiTietPhieuNhapId = line.Id, KhoId = order.KhoId, GiaMua = line.DonGiaNhap, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            }
        }
        order.TrangThai = "DA_XAC_NHAN"; order.XacNhanLuc = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(order.Id, $"purchase-orders/{order.Id}", order.TrangThai, order.TongThanhToan);
    }

    public async Task<IssueResponse> CreateIssueAsync(CreateIssueRequest r, CancellationToken ct) { if (r.Lines is null || r.Lines.Count == 0) throw new InvalidArgumentException("A stock issue cannot be empty."); var issue = new PhieuXuat { MaPhieuXuat = $"PX{Guid.NewGuid():N}"[..40], KhoId = r.WarehouseId, NhanVienTaoId = r.EmployeeId, MucDich = r.Purpose, NguoiNhan = r.Recipient, LyDo = r.Reason, NgayXuat = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ChiTiet = r.Lines.Select(x => new ChiTietPhieuXuat { DanhMucHangHoaId = x.ItemId, SoLuong = x.Quantity }).ToList() }; db.PhieuXuats.Add(issue); await db.SaveChangesAsync(ct); return new(issue.Id, $"stock-issues/{issue.Id}", issue.TrangThai); }

    // Xác nh?n xu?t sau khi ki?m tra s? d? kh? d?ng và ghi bút toán gi?m t?n.
    public async Task<IssueResponse> ConfirmIssueAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var issue = await db.PhieuXuats.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException($"Stock issue {id} was not found.");
        if (issue.TrangThai != "NHAP") throw new InvalidArgumentException("Only a draft stock issue can be confirmed.");
        var itemIds = issue.ChiTiet.Select(x => x.DanhMucHangHoaId).ToArray();
        var items = await db.DanhMucHangHoas.Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var itemBalances = await db.GiaoDichKhos
            .Where(x => x.KhoId == issue.KhoId && itemIds.Contains(x.DanhMucHangHoaId))
            .GroupBy(x => x.DanhMucHangHoaId)
            .Select(x => new { x.Key, Balance = x.Sum(v => v.SoLuongThayDoi) })
            .ToDictionaryAsync(x => x.Key, x => x.Balance, ct);
        var demandedByItem = issue.ChiTiet.GroupBy(x => x.DanhMucHangHoaId)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.SoLuong));
        foreach (var (itemId, demand) in demandedByItem)
        {
            if (!items.TryGetValue(itemId, out var item))
                throw new ResourceNotFoundException($"Inventory item {itemId} was not found.");
            if (!itemBalances.TryGetValue(itemId, out var available) || available < demand)
                throw new AlreadyExistsException("Insufficient available stock for the stock issue.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lotBalances = new Dictionary<long, decimal>();
        var lotsByItem = new Dictionary<long, List<LoHang>>();
        foreach (var item in items.Values.Where(x => x.TheoDoiLo))
        {
            var lots = await db.LoHangs
                .Where(x => x.KhoId == issue.KhoId && x.DanhMucHangHoaId == item.Id &&
                    (x.HanSuDung == null || x.HanSuDung >= today))
                .OrderBy(x => x.HanSuDung == null)
                .ThenBy(x => x.HanSuDung)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);
            lotsByItem[item.Id] = lots;
            var lotIds = lots.Select(x => x.Id).ToArray();
            var balances = await db.GiaoDichKhos.Where(x => x.LoHangId.HasValue && lotIds.Contains(x.LoHangId.Value))
                .GroupBy(x => x.LoHangId!.Value)
                .Select(x => new { LotId = x.Key, Balance = x.Sum(v => v.SoLuongThayDoi) })
                .ToDictionaryAsync(x => x.LotId, x => x.Balance, ct);
            foreach (var lot in lots)
                lotBalances[lot.Id] = balances.GetValueOrDefault(lot.Id);
        }

        foreach (var line in issue.ChiTiet)
        {
            var item = items[line.DanhMucHangHoaId];
            if (item.TheoDoiLo)
            {
                var remaining = line.SoLuong;
                foreach (var lot in lotsByItem[item.Id])
                {
                    var take = Math.Min(remaining, Math.Max(0, lotBalances[lot.Id]));
                    if (take <= 0) continue;
                    db.GiaoDichKhos.Add(new GiaoDichKho { MaGiaoDich = $"XK{Guid.NewGuid():N}"[..50], KhoId = issue.KhoId, DanhMucHangHoaId = item.Id, LoHangId = lot.Id, PhieuXuatId = issue.Id, ChiTietPhieuXuatId = line.Id, NhanVienId = issue.NhanVienXacNhanId, LoaiGiaoDich = "XUAT", SoLuongThayDoi = -take, ThoiGian = DateTime.UtcNow });
                    lotBalances[lot.Id] -= take;
                    remaining -= take;
                    if (remaining <= 0) break;
                }
                if (remaining > 0) throw new AlreadyExistsException("Insufficient non-expired lot stock for FEFO issue.");
            }
            else db.GiaoDichKhos.Add(new GiaoDichKho { MaGiaoDich = $"XK{Guid.NewGuid():N}"[..50], KhoId = issue.KhoId, DanhMucHangHoaId = item.Id, PhieuXuatId = issue.Id, ChiTietPhieuXuatId = line.Id, NhanVienId = issue.NhanVienXacNhanId, LoaiGiaoDich = "XUAT", SoLuongThayDoi = -line.SoLuong, ThoiGian = DateTime.UtcNow });
        }
        issue.TrangThai = "DA_XAC_NHAN"; issue.XacNhanLuc = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(issue.Id, $"stock-issues/{issue.Id}", issue.TrangThai);
    }
    public async Task<StockBalanceResponse> GetBalanceAsync(long warehouseId, long itemId, CancellationToken ct) => new(warehouseId, itemId, await db.GiaoDichKhos.Where(x => x.KhoId == warehouseId && x.DanhMucHangHoaId == itemId).SumAsync(x => x.SoLuongThayDoi, ct));
}
