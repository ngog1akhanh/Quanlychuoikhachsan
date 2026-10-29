using System.ComponentModel.DataAnnotations;

namespace Quanlychuoikhachsan.Application.Inventory;

public sealed record SupplierRequest(
    [param: Required, StringLength(30)] string Code,
    [param: Required, StringLength(150)] string Name,
    [param: StringLength(120)] string? Contact,
    [param: StringLength(20)] string? Phone,
    [param: EmailAddress, StringLength(150)] string? Email,
    [param: StringLength(255)] string? Address,
    [param: StringLength(50)] string? TaxCode,
    [param: StringLength(500)] string? Note,
    string Status = "HOAT_DONG");
public sealed record WarehouseRequest(string Code, string Name, string Type, string? Location, decimal? MinimumTemperature, decimal? MaximumTemperature, string Status = "HOAT_DONG");
public sealed record LookupRequest(string Code, string Name, string? Description, string Status = "HOAT_DONG");
public sealed record UpdatePurchaseRequest(long SupplierId, long WarehouseId, decimal Discount, decimal Tax,
    IReadOnlyList<PurchaseLineRequest> Lines, string? Note, string? SupplierInvoiceNumber = null, DateOnly? SupplierInvoiceDate = null);
public sealed record StockAdjustmentRequest(long WarehouseId, long ItemId, long? LotId, decimal QuantityChange, string Type, string Reason);
public sealed record AssetMoveRequest(long? RoomId, long? WarehouseId, string? OtherLocation, string Reason);
public sealed record AssetConditionRequest(string Condition, string Reason);
