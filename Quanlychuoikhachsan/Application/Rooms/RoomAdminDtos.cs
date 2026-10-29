namespace Quanlychuoikhachsan.Application.Rooms;

// ---------------- DTO (mục 9: quản lý phòng, sơ đồ phòng, lịch phòng) ----------------
public sealed record RoomBoardItem(
    long Id,
    string RoomCode,
    string RoomNumber,
    string? Floor,
    string? Direction,
    string? View,
    string? Position,
    long RoomTypeId,
    string RoomTypeCode,
    string RoomTypeName,
    decimal BaseNightlyPrice,
    decimal NightSurcharge,
    string Status,
    string? Description,
    string? Note,
    long? CurrentBookingId,
    string? CurrentBookingCode,
    string? CurrentGuest,
    DateTime? CurrentCheckOut,
    long? ArrivalBookingId,
    string? ArrivalBookingCode,
    string? ArrivalGuest,
    DateTime? ArrivalTime);

public sealed record RoomTypeSummary(
    long Id,
    string Code,
    string Name,
    int MaxAdults,
    int MaxChildren,
    decimal? AreaM2,
    decimal BaseNightlyPrice,
    string? Description,
    string Status,
    int RoomCount,
    int ReadyCount,
    int OccupiedCount);

public sealed record UpdateRoomRequest(
    string? RoomNumber = null,
    long? RoomTypeId = null,
    string? Floor = null,
    string? Direction = null,
    string? View = null,
    string? Position = null,
    decimal? NightSurcharge = null,
    string? Description = null,
    string? Note = null);

public sealed record SetRoomStatusRequest(string Status, string? Note = null);

public sealed record UpdateRoomTypeRequest(
    string? Name = null,
    int? MaxAdults = null,
    int? MaxChildren = null,
    decimal? AreaM2 = null,
    decimal? BaseNightlyPrice = null,
    string? Description = null,
    string? Status = null);

public sealed record RoomCalendarRoom(long Id, string RoomNumber, string? Floor, string RoomTypeCode, string Status);

public sealed record RoomCalendarCell(long RoomId, DateOnly Date, string LockStatus, long BookingId, string BookingCode, string BookingStatus, string Guest);

public sealed record RoomCalendar(DateOnly From, DateOnly To, IReadOnlyList<RoomCalendarRoom> Rooms, IReadOnlyList<RoomCalendarCell> Cells);

public interface IRoomAdminService
{
    Task<IReadOnlyList<RoomBoardItem>> GetBoardAsync(CancellationToken ct);
    Task<IReadOnlyList<RoomTypeSummary>> GetRoomTypesAsync(CancellationToken ct);
    Task<RoomBoardItem> UpdateRoomAsync(long roomId, UpdateRoomRequest request, CancellationToken ct);
    Task<RoomBoardItem> SetStatusAsync(long roomId, SetRoomStatusRequest request, CancellationToken ct);
    Task<RoomTypeSummary> UpdateRoomTypeAsync(long roomTypeId, UpdateRoomTypeRequest request, CancellationToken ct, bool replaceArea = false);
    Task<RoomCalendar> GetCalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);
}

