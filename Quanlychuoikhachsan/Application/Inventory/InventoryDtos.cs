namespace Quanlychuoikhachsan.Application.Inventory;

public sealed record InventoryItemResponse(long Id, string Code, string Name, string Type, long UnitId, decimal ReferenceCost, bool TrackLot, bool TrackExpiry, bool TrackIndividual, decimal Balance);
public sealed record CreateInventoryItemRequest(string Code, string Name, string Type, long UnitId, long? GroupId, decimal ReferenceCost, decimal MinimumStock, bool TrackLot, bool TrackExpiry, bool TrackIndividual, string? StorageCondition, string? Description);
public sealed record PurchaseLineRequest(long ItemId, decimal Quantity, decimal UnitPrice, string? LotCode, DateOnly? ManufactureDate, DateOnly? ExpiryDate, IReadOnlyList<string>? Serials);
public sealed record CreatePurchaseRequest(long SupplierId, long WarehouseId, long EmployeeId, decimal Discount, decimal Tax, IReadOnlyList<PurchaseLineRequest> Lines, string? Note);
public sealed record PurchaseResponse(long Id, string Name, string Status, decimal Total);
public sealed record IssueLineRequest(long ItemId, decimal Quantity);
public sealed record CreateIssueRequest(long WarehouseId, long EmployeeId, string Purpose, IReadOnlyList<IssueLineRequest> Lines, string? Recipient, string? Reason);
public sealed record IssueResponse(long Id, string Name, string Status);
public sealed record StockBalanceResponse(long WarehouseId, long ItemId, decimal Balance);
public sealed record ConfirmPurchaseLineRequest(long DetailId, string? LotCode, DateOnly? ManufactureDate, DateOnly? ExpiryDate, IReadOnlyList<string>? Serials);
public sealed record ConfirmPurchaseRequest(IReadOnlyList<ConfirmPurchaseLineRequest> Lines);
