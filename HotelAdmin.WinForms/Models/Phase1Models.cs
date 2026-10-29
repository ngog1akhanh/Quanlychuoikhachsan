namespace HotelAdmin.Models;

// DTO Giai đoạn 1 – khớp với backend:
//   Application/FrontDesk/FrontDeskDtos.cs, Application/FrontDesk/FrontDeskService.cs (CancellationRequestItem)
//   Application/Rooms/RoomAdminService.cs
// Thời gian nhận/trả phòng là giờ địa phương của khách sạn (không kèm múi giờ).

// ---------------- Phòng ----------------
public sealed record RoomBoardItem(
    long Id, string RoomCode, string RoomNumber, string? Floor, string? Direction, string? View, string? Position,
    long RoomTypeId, string RoomTypeCode, string RoomTypeName, decimal BaseNightlyPrice, decimal NightSurcharge,
    string Status, string? Description, string? Note,
    long? CurrentBookingId, string? CurrentBookingCode, string? CurrentGuest, DateTime? CurrentCheckOut,
    long? ArrivalBookingId, string? ArrivalBookingCode, string? ArrivalGuest, DateTime? ArrivalTime);

public sealed record RoomTypeSummary(
    long Id, string Code, string Name, int MaxAdults, int MaxChildren, decimal? AreaM2, decimal BaseNightlyPrice,
    string? Description, string Status, int RoomCount, int ReadyCount, int OccupiedCount);

public sealed record UpdateRoomRequest(
    string? RoomNumber = null, long? RoomTypeId = null, string? Floor = null, string? Direction = null, string? View = null,
    string? Position = null, decimal? NightSurcharge = null, string? Description = null, string? Note = null);

public sealed record SetRoomStatusRequest(string Status, string? Note = null);

public sealed record UpdateRoomTypeRequest(
    string? Name = null, int? MaxAdults = null, int? MaxChildren = null, decimal? AreaM2 = null,
    decimal? BaseNightlyPrice = null, string? Description = null, string? Status = null);

public sealed record CreateRoomTypeRequest(string Code, string Name, int MaxAdults, int MaxChildren, decimal BaseNightlyPrice, string? Description);

public sealed record CreateRoomRequest(string RoomCode, string RoomNumber, long RoomTypeId, string? Floor, string? ViewType, decimal DefaultNightSurcharge);

/// <summary>Phong bì {data, request_id} mà một số API tạo mới trả về.</summary>
public sealed record ApiEnvelope<T>(T Data, string? RequestId);

public sealed record RoomCalendarRoom(long Id, string RoomNumber, string? Floor, string RoomTypeCode, string Status);

public sealed record RoomCalendarCell(long RoomId, DateOnly Date, string LockStatus, long BookingId, string BookingCode, string BookingStatus, string Guest);

public sealed record RoomCalendar(DateOnly From, DateOnly To, List<RoomCalendarRoom> Rooms, List<RoomCalendarCell> Cells);

// ---------------- Booking ----------------
public sealed record FrontDeskBookingItem(
    long Id, string BookingCode, string Status, string Channel, long CustomerId, string CustomerName, string? CustomerPhone,
    string Rooms, DateTime CheckIn, DateTime CheckOut, int Nights, int Adults, int Children, decimal Total,
    decimal DepositPaid, DateTime BookedAt);

public sealed record FrontDeskBookingPage(List<FrontDeskBookingItem> Items, int Total, int Page, int PageSize);

public sealed record BookingRoomLine(
    long DetailId, long RoomId, string RoomNumber, string RoomTypeName, string RoomStatus, string DetailStatus,
    decimal Amount, DateTime? CheckedInAt, DateTime? CheckedOutAt);

public sealed record BookingNightLine(long DetailId, string RoomNumber, DateOnly StayDate, decimal BasePrice, decimal Surcharge, decimal Adjustment, decimal FinalPrice);

public sealed record StayGuestDto(
    long? Id, long DetailId, string FullName, DateOnly? BirthDate, string? Gender, string? DocumentType,
    string? DocumentNumber, string? Nationality, bool IsRepresentative);

public sealed record DepositLine(long Id, string Code, decimal Amount, string MethodName, DateTime PaidAt, string Status);

public sealed record InvoiceLine(
    long Id, string Code, DateTime IssuedAt, decimal RoomAmount, decimal Surcharge, decimal Compensation, decimal Vat,
    decimal Total, decimal DepositApplied, decimal Paid, decimal Due, string Status);

public sealed record StatusHistoryLine(DateTime At, string? From, string To, string? By, string? Reason);

public sealed record CancellationLine(long Id, string Code, string Reason, DateTime RequestedAt, decimal ExpectedRefund, decimal ApprovedRefund, string Status, string? Note);

public sealed record FrontDeskCustomer(long Id, string Code, string FullName, string? Phone, string? Email, string? IdentityNumber, string? Address, int Stays);

public sealed record FrontDeskBookingDetail(
    FrontDeskBookingItem Summary, FrontDeskCustomer Customer, string? SpecialRequest, DateTime? HoldExpiresAt,
    List<BookingRoomLine> Rooms, List<BookingNightLine> Nights, List<StayGuestDto> Guests, List<DepositLine> Deposits,
    List<InvoiceLine> Invoices, List<CancellationLine> Cancellations, List<StatusHistoryLine> History);

public sealed record NewCustomerRequest(string FullName, string? Phone, string? Email, string? IdentityNumber, string? Address);

public sealed record DeskDepositRequest(long PaymentMethodId, decimal Amount, string? Note = null, string? Reference = null);

public sealed record CreateDeskBookingRequest(
    long? CustomerId, NewCustomerRequest? NewCustomer, List<long> RoomIds, DateTime CheckIn, DateTime CheckOut,
    int Adults, int Children, string Channel, string? SpecialRequest, DeskDepositRequest? Deposit);

public sealed record ChangeDatesRequest(DateTime CheckIn, DateTime CheckOut, string? Reason);

public sealed record ChangeRoomRequest(long DetailId, long NewRoomId, string? Reason);

public sealed record DeskCancelRequest(string Reason);

public sealed record DeskNoteRequest(string? Note);

public sealed record DecideCancellationRequest(decimal ApprovedRefund, string? Note);

public sealed record ChangeResult(FrontDeskBookingDetail Booking, decimal OldTotal, decimal NewTotal, string Message);

public sealed record AvailableRoom(
    long Id, string RoomNumber, string? Floor, string? View, long RoomTypeId, string RoomTypeName, int MaxAdults,
    int MaxChildren, decimal BaseNightlyPrice, decimal NightSurcharge, string Status);

public sealed record NightPrice(DateOnly StayDate, decimal BasePrice, decimal RoomSurcharge, long? PricingPolicyId, decimal Adjustment, decimal FinalPrice);

public sealed record PricePreview(long RoomId, DateOnly CheckInDate, DateOnly CheckOutDate, List<NightPrice> Nights, decimal Total);

public sealed record PaymentMethodItem(long Id, string Code, string Name);

public sealed record CancellationRequestItem(
    long Id, string Code, long BookingId, string BookingCode, string CustomerName, string Reason,
    DateTime RequestedAt, decimal ExpectedRefund, decimal ApprovedRefund, string Status, string? Note);

// ---------------- Lễ tân ----------------
public sealed record FrontDeskDay(DateOnly Date, List<FrontDeskBookingItem> Arrivals, List<FrontDeskBookingItem> Departures, List<FrontDeskBookingItem> InHouse);

public sealed record DeskCheckInRequest(List<StayGuestDto>? Guests, bool AllowNotReadyRooms = false);

public sealed record CheckOutPreview(
    long BookingId, string BookingCode, DateTime PlannedCheckIn, DateTime PlannedCheckOut, DateTime? ActualCheckIn,
    DateTime Now, decimal RoomAmount, decimal EarlyCheckInSurcharge, decimal LateCheckOutSurcharge, decimal Compensation,
    decimal VatRate, decimal Deposit, decimal EstimatedTotal, decimal EstimatedDue, List<string> Notes);

public sealed record DeskCheckOutRequest(decimal Surcharge, decimal VatRate = 0.1m);

public sealed record DeskCheckOutResult(InvoiceLine Invoice, List<string> RoomsToClean, string Message);

// Hợp đồng hóa đơn dùng chung với API mới; không tự ghi nhận thanh toán VietQR.
public sealed record RoomInvoice(long Id, string Name, long BookingId, decimal RoomAmount, decimal Surcharge,
    decimal Compensation, decimal Vat, decimal Total, decimal DepositApplied, decimal Paid, decimal Due, string Status);
public sealed record InvoicePayment(long PaymentMethodId, decimal Amount, string TransactionId,
    string PaymentGateway = "MANUAL", long? EmployeeId = null, long? ShiftId = null);
