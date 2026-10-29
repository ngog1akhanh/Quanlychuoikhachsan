using System.Data;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.Inventory;

public sealed partial class InventoryService
{
    // All stock writers lock the warehouse before reading the ledger.
    private async Task LockWarehouseAsync(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Khos.FromSqlInterpolated($"SELECT * FROM kho WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        if (!await db.Khos.AnyAsync(x => x.Id == id && x.TrangThai == "HOAT_DONG", ct))
            throw new ResourceNotFoundException("Active warehouse was not found.");
    }
    private async Task LockPurchaseAsync(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.PhieuNhaps.FromSqlInterpolated($"SELECT * FROM phieu_nhap WHERE id = {id} FOR UPDATE").ToListAsync(ct);
    }
    private async Task LockIssueAsync(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.PhieuXuats.FromSqlInterpolated($"SELECT * FROM phieu_xuat WHERE id = {id} FOR UPDATE").ToListAsync(ct);
    }

    private async Task ValidateItemAsync(CreateInventoryItemRequest r, CancellationToken ct)
    {
        Input.Text(r.Code, 40, "code"); Input.Text(r.Name, 150, "name");
        if ((r.TrackExpiry && !r.TrackLot) || (r.TrackIndividual && r.Type != "TAI_SAN"))
            throw new InvalidArgumentException("Inventory item tracking flags violate K1 constraints.");
        Input.OneOf(r.Type, "THUC_PHAM", "DO_UONG", "HANG_MINIBAR", "VAT_TU", "DO_VAI", "TAI_SAN", "KHAC");
        Input.Money(r.ReferenceCost, "reference_cost"); Input.Money(r.MinimumStock, "minimum_stock");
        Input.Optional(r.Description, 700, "description"); Input.Optional(r.StorageCondition, 255, "storage_condition");
        if (!await db.DonViTinhs.AnyAsync(x => x.Id == r.UnitId && x.TrangThai == "HOAT_DONG", ct))
            throw new ResourceNotFoundException("Active unit was not found.");
        if (r.GroupId.HasValue && !await db.NhomHangs.AnyAsync(x => x.Id == r.GroupId && x.TrangThai == "HOAT_DONG", ct))
            throw new ResourceNotFoundException("Active item group was not found.");
    }

    public async Task<DanhMucHangHoa> UpdateItemAsync(long id, CreateInventoryItemRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await ValidateItemAsync(r, ct);
        var item = await db.DanhMucHangHoas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Item was not found.");
        if ((item.DonViTinhId != r.UnitId || item.LoaiHang != r.Type || item.TheoDoiLo != r.TrackLot || item.TheoDoiHanSuDung != r.TrackExpiry || item.TheoDoiCaThe != r.TrackIndividual)
            && (await db.ChiTietPhieuNhaps.AnyAsync(x => x.DanhMucHangHoaId == id, ct) || await db.ChiTietPhieuXuats.AnyAsync(x => x.DanhMucHangHoaId == id, ct) || await db.GiaoDichKhos.AnyAsync(x => x.DanhMucHangHoaId == id, ct)))
            throw new InvalidArgumentException("Unit, type and tracking cannot change after an item appears in stock documents.");
        var code = Input.Text(r.Code, 40, "code");
        if (await db.DanhMucHangHoas.AnyAsync(x => x.Id != id && x.MaHang == code, ct)) throw new AlreadyExistsException("Item code already exists.");
        item.MaHang = code; item.TenHang = r.Name.Trim(); item.LoaiHang = r.Type; item.DonViTinhId = r.UnitId; item.NhomHangId = r.GroupId;
        item.GiaVonThamChieu = r.ReferenceCost; item.MucTonToiThieu = r.MinimumStock; item.TheoDoiLo = r.TrackLot; item.TheoDoiHanSuDung = r.TrackExpiry; item.TheoDoiCaThe = r.TrackIndividual;
        item.DieuKienBaoQuan = r.StorageCondition; item.MoTa = r.Description; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return item;
    }

    public async Task<PhieuXuat> UpdateIssueAsync(long id, CreateIssueRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockIssueAsync(id, ct);
        var issue = await db.PhieuXuats.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Issue was not found.");
        if (issue.TrangThai != "NHAP") throw new InvalidArgumentException("Only draft issues can be edited.");
        await ValidateIssueAsync(r, ct);
        db.ChiTietPhieuXuats.RemoveRange(issue.ChiTiet);
        issue.ChiTiet = r.Lines.Select(x => new ChiTietPhieuXuat { DanhMucHangHoaId = x.ItemId, SoLuong = x.Quantity }).ToList();
        issue.KhoId = r.WarehouseId; issue.MucDich = r.Purpose; issue.NguoiNhan = r.Recipient; issue.LyDo = r.Reason; issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return issue;
    }

    private async Task ValidatePurchaseAsync(long supplierId, long warehouseId, decimal discount, decimal tax,
        IReadOnlyList<PurchaseLineRequest>? lines, string? note, CancellationToken ct)
    {
        Input.Money(discount, "discount"); Input.Money(tax, "tax"); Input.Optional(note, 500, "note");
        if (lines is null || lines.Count is 0 or > 500) throw new InvalidArgumentException("A purchase needs 1–500 lines.");
        if (!await db.NhaCungCaps.AnyAsync(x => x.Id == supplierId && x.TrangThai == "HOAT_DONG", ct))
            throw new ResourceNotFoundException("Active supplier was not found.");
        if (!await db.Khos.AnyAsync(x => x.Id == warehouseId && x.TrangThai == "HOAT_DONG", ct))
            throw new ResourceNotFoundException("Active warehouse was not found.");
        var ids = lines.Select(x => x.ItemId).Distinct().ToArray();
        var items = await db.DanhMucHangHoas.Where(x => ids.Contains(x.Id) && x.TrangThai == "HOAT_DONG").ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != ids.Length) throw new ResourceNotFoundException("One or more active items were not found.");
        foreach (var line in lines)
        {
            Input.Quantity(line.Quantity); Input.Money(line.UnitPrice, "unit_price");
            Input.Money(Input.Round(line.Quantity * line.UnitPrice), "line_total");
            if (items[line.ItemId].TheoDoiCaThe && (line.Quantity != decimal.Truncate(line.Quantity) || line.Quantity > 10000))
                throw new InvalidArgumentException("Individually tracked assets require an integer quantity up to 10000 per line.");
            if (line.LotCode is not null || line.ExpiryDate is not null || line.ManufactureDate is not null || line.Serials is { Count: > 0 })
                throw new InvalidArgumentException("Provide lot dates and serials when confirming receipt, not in draft lines.");
        }
        var subtotal = lines.Sum(x => Input.Round(x.Quantity * x.UnitPrice));
        Input.Money(subtotal, "subtotal");
        if (discount > subtotal) throw new InvalidArgumentException("Discount exceeds purchase subtotal.");
        Input.Money(subtotal - discount + tax, "total");
    }

    private async Task ValidateReceivingAsync(PhieuNhap order, Dictionary<long, DanhMucHangHoa> items,
        Dictionary<long, ConfirmPurchaseLineRequest> allocations, CancellationToken ct)
    {
        await LockWarehouseAsync(order.KhoId, ct);
        var lotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in order.ChiTiet)
        {
            if (!items.TryGetValue(line.DanhMucHangHoaId, out var item) || item.TrangThai != "HOAT_DONG")
                throw new InvalidArgumentException("Cannot receive an inactive or missing item.");
            var a = allocations[line.Id];
            Input.Quantity(line.SoLuong); Input.Money(line.DonGiaNhap, "unit_price");
            if (line.ThanhTien != Input.Round(line.SoLuong * line.DonGiaNhap)) throw new InvalidArgumentException("Invalid stored line total.");
            if (item.TheoDoiHanSuDung && a.ExpiryDate is null) throw new InvalidArgumentException("Expiry date is required.");
            if (item.TheoDoiLo)
            {
                var code = Input.Text(a.LotCode, 80, "lot_code");
                if (a.ExpiryDate < a.ManufactureDate || a.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow))
                    throw new InvalidArgumentException("Lot dates are invalid or the lot has expired.");
                if (!lotKeys.Add($"{item.Id}:{code}") || await db.LoHangs.AnyAsync(x => x.KhoId == order.KhoId && x.DanhMucHangHoaId == item.Id && x.MaLo == code, ct))
                    throw new AlreadyExistsException("Lot code already exists for this warehouse and item.");
            }
            else if (a.LotCode is not null || a.ManufactureDate is not null || a.ExpiryDate is not null)
                throw new InvalidArgumentException("This item does not track lots.");
            if (item.TheoDoiCaThe)
            {
                if (line.SoLuong != decimal.Truncate(line.SoLuong) || line.SoLuong > 10000 || (a.Serials is not null && a.Serials.Count != line.SoLuong))
                    throw new InvalidArgumentException("Asset quantity must be integral and serial count must match quantity.");
                foreach (var serial in a.Serials ?? [])
                {
                    var value = Input.Text(serial, 100, "serial");
                    if (!serials.Add(value) || await db.TaiSanCaThes.AnyAsync(x => x.SoSerial == value, ct))
                        throw new AlreadyExistsException("Serial is already in use.");
                }
            }
            else if (a.Serials is { Count: > 0 }) throw new InvalidArgumentException("This item does not track individual assets.");
        }
    }

    private async Task ValidateIssueAsync(CreateIssueRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Purpose, "BUONG_PHONG", "MINIBAR", "DICH_VU", "BEP", "SUA_CHUA", "KHAC");
        Input.Optional(r.Recipient, 150, "recipient"); Input.Optional(r.Reason, 500, "reason");
        if (r.Lines is null || r.Lines.Count is 0 or > 500) throw new InvalidArgumentException("An issue needs 1–500 lines.");
        if (!await db.Khos.AnyAsync(x => x.Id == r.WarehouseId && x.TrangThai == "HOAT_DONG", ct)) throw new ResourceNotFoundException("Active warehouse was not found.");
        foreach (var line in r.Lines)
        {
            Input.Quantity(line.Quantity);
            var item = await db.DanhMucHangHoas.SingleOrDefaultAsync(x => x.Id == line.ItemId && x.TrangThai == "HOAT_DONG", ct)
                ?? throw new ResourceNotFoundException("Active item was not found.");
            if (item.TheoDoiCaThe) throw new InvalidArgumentException("Use the asset transfer API to issue individual assets.");
        }
    }

    public async Task<PhieuNhap> UpdatePurchaseAsync(long id, UpdatePurchaseRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockPurchaseAsync(id, ct);
        var order = await db.PhieuNhaps.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Purchase was not found.");
        if (order.TrangThai != "NHAP") throw new InvalidArgumentException("Only draft purchases can be edited.");
        await ValidatePurchaseAsync(r.SupplierId, r.WarehouseId, r.Discount, r.Tax, r.Lines, r.Note, ct);
        Input.Optional(r.SupplierInvoiceNumber, 100, "supplier_invoice_number");
        db.ChiTietPhieuNhaps.RemoveRange(order.ChiTiet);
        order.ChiTiet = r.Lines.Select(x => new ChiTietPhieuNhap { DanhMucHangHoaId = x.ItemId, SoLuong = x.Quantity, DonGiaNhap = x.UnitPrice, ThanhTien = Input.Round(x.Quantity * x.UnitPrice) }).ToList();
        order.NhaCungCapId = r.SupplierId; order.KhoId = r.WarehouseId; order.ChietKhau = r.Discount; order.TienThue = r.Tax;
        order.TongTienHang = order.ChiTiet.Sum(x => x.ThanhTien); order.TongThanhToan = order.TongTienHang - r.Discount + r.Tax;
        order.GhiChu = r.Note; order.SoHoaDonNcc = r.SupplierInvoiceNumber; order.NgayHoaDonNcc = r.SupplierInvoiceDate; order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }

    public async Task CancelDocumentAsync(long id, bool purchase, string reason, CancellationToken ct)
    {
        Input.Text(reason, 500, "reason");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (purchase)
        {
            await LockPurchaseAsync(id, ct);
            var row = await db.PhieuNhaps.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Purchase was not found.");
            if (row.TrangThai != "NHAP") throw new InvalidArgumentException("Only a draft can be cancelled.");
            row.TrangThai = "DA_HUY"; row.GhiChu = reason; row.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await LockIssueAsync(id, ct);
            var row = await db.PhieuXuats.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Issue was not found.");
            if (row.TrangThai != "NHAP") throw new InvalidArgumentException("Only a draft can be cancelled.");
            row.TrangThai = "DA_HUY"; row.GhiChu = reason; row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<GiaoDichKho> AdjustAsync(StockAdjustmentRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Type, "DIEU_CHINH", "HONG_MAT", "THANH_LY"); Input.Text(r.Reason, 500, "reason");
        Input.Quantity(Math.Abs(r.QuantityChange));
        if (r.Type != "DIEU_CHINH" && r.QuantityChange >= 0) throw new InvalidArgumentException("Loss/disposal quantities must be negative.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockWarehouseAsync(r.WarehouseId, ct);
        var item = await db.DanhMucHangHoas.SingleOrDefaultAsync(x => x.Id == r.ItemId, ct) ?? throw new ResourceNotFoundException("Item was not found.");
        if (item.TheoDoiCaThe) throw new InvalidArgumentException("Use the asset condition API for individual assets.");
        if (item.TheoDoiLo != r.LotId.HasValue) throw new InvalidArgumentException("Lot tracking does not match the item.");
        if (r.LotId.HasValue && !await db.LoHangs.AnyAsync(x => x.Id == r.LotId && x.KhoId == r.WarehouseId && x.DanhMucHangHoaId == r.ItemId, ct))
            throw new InvalidArgumentException("Lot does not belong to the warehouse and item.");
        var balance = await db.GiaoDichKhos.Where(x => x.KhoId == r.WarehouseId && x.DanhMucHangHoaId == r.ItemId && (!r.LotId.HasValue || x.LoHangId == r.LotId)).SumAsync(x => (decimal?)x.SoLuongThayDoi, ct) ?? 0;
        if (balance + r.QuantityChange < 0) throw new AlreadyExistsException("Adjustment would make stock negative.");
        var ledger = new GiaoDichKho { MaGiaoDich = $"DC{Guid.NewGuid():N}", KhoId = r.WarehouseId, DanhMucHangHoaId = r.ItemId, LoHangId = r.LotId, LoaiGiaoDich = r.Type, SoLuongThayDoi = r.QuantityChange, GhiChu = r.Reason, NhanVienId = await actor.EmployeeIdAsync(ct), ThoiGian = DateTime.UtcNow };
        db.GiaoDichKhos.Add(ledger); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ledger;
    }
}
