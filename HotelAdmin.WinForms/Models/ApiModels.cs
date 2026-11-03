namespace HotelAdmin.Models;

// DTO khớp với backend (Application/*Dtos.cs). Tên property PascalCase,
// ApiClient tự chuyển sang snake_case khi (de)serialize.

public sealed record LoginRequest(string Identifier, string Password);

public sealed record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string CustomerName);

public sealed record MeRole(string Code, string Name);

public sealed record MeResponse(
    long AccountId,
    string DisplayName,
    string AccountType,
    string? Email,
    long? StaffId,
    string? StaffCode,
    List<MeRole> Roles,
    List<string> Permissions);

public sealed record DashboardResponse(
    DateOnly From, DateOnly To,
    decimal RoomRevenue, decimal ServiceRevenue, decimal Compensation, decimal OutstandingInvoices,
    int Bookings, int OccupiedRoomNights, int AvailableRoomNights,
    decimal Occupancy, decimal Adr, decimal RevPar);

public sealed record RevenueReportResponse(
    DateOnly From, DateOnly To,
    decimal RoomRevenue, decimal ServiceRevenue, decimal Compensation, decimal Vat, decimal TotalRevenue);

public sealed record OccupancyReportResponse(
    DateOnly From, DateOnly To,
    int AvailableRoomNights, int OccupiedRoomNights, decimal Occupancy, decimal Adr, decimal RevPar);

public sealed record RoomResponse(long Id, string Name, string RoomCode, string RoomNumber, long RoomTypeId, string RoomTypeCode, string Status);

public sealed record BookingListItem(long Id, string Name, string BookingCode, string Status, DateTime CheckIn, DateTime CheckOut, decimal Total);

public sealed record PageResponse<T>(List<T> Items, string? NextPageToken);
