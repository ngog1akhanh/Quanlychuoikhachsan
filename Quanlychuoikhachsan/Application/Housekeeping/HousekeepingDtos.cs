namespace Quanlychuoikhachsan.Application.Housekeeping;

public sealed record HousekeepingTaskResponse(long Id, string Name, long RoomId, long? BookingId, string Type, string Status);
public sealed record CreateRoomConditionRequest(long RoomId, long? BookingId, long? AssetId, string Type, string Severity, string Description, decimal EstimatedCost, decimal ActualCost, bool CausedByGuest, string? Evidence);
public sealed record RoomConditionResponse(long Id, long RoomId, string Type, string Status, decimal CompensationCost, bool CausedByGuest);
public sealed record CheckoutRequest(long BookingId, long RoomId, long EmployeeId);
