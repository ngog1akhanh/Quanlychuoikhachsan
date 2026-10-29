using System.ComponentModel.DataAnnotations;

namespace Quanlychuoikhachsan.Application.FrontDesk;

// DTO cho nghiệp vụ lễ tân (Giai đoạn 1). JSON body/response tự chuyển sang snake_case.
// Adapter nhận giờ địa phương của khách sạn và chuyển sang UTC để lưu;
// client gửi lên dạng "2026-10-20T14:00:00" không kèm múi giờ.

// ---------------- Tra cứu booking ----------------
public sealed record FrontDeskBookingQuery(
    string? Q = null,
    string? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record FrontDeskBookingItem(
    long Id,
    string BookingCode,
    string Status,
    string Channel,
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string Rooms,
    DateTime CheckIn,
    DateTime CheckOut,
    int Nights,
    int Adults,
    int Children,
    decimal Total,
    decimal DepositPaid,
    DateTime BookedAt);

public sealed record FrontDeskBookingPage(IReadOnlyList<FrontDeskBookingItem> Items, int Total, int Page, int PageSize);

// ---------------- Chi tiết booking ----------------
public sealed record BookingRoomLine(
    long DetailId,
    long RoomId,
    string RoomNumber,
    string RoomTypeName,
    string RoomStatus,
    string DetailStatus,
    decimal Amount,
    DateTime? CheckedInAt,
    DateTime? CheckedOutAt);

public sealed record BookingNightLine(long DetailId, string RoomNumber, DateOnly StayDate, decimal BasePrice, decimal Surcharge, decimal Adjustment, decimal FinalPrice);

public sealed record StayGuestDto(
    long? Id,
    long DetailId,
    [param: Required, StringLength(120)] string FullName,
    DateOnly? BirthDate,
    string? Gender,
    string? DocumentType,
    string? DocumentNumber,
    string? Nationality,
    bool IsRepresentative);

public sealed record DepositLine(long Id, string Code, decimal Amount, string MethodName, DateTime PaidAt, string Status);

public sealed record InvoiceLine(long Id, string Code, DateTime IssuedAt, decimal RoomAmount, decimal Surcharge, decimal Compensation, decimal Vat, decimal Total, decimal DepositApplied, decimal Paid, decimal Due, string Status);

public sealed record StatusHistoryLine(DateTime At, string? From, string To, string? By, string? Reason);

public sealed record CancellationLine(long Id, string Code, string Reason, DateTime RequestedAt, decimal ExpectedRefund, decimal ApprovedRefund, string Status, string? Note);

public sealed record FrontDeskCustomer(long Id, string Code, string FullName, string? Phone, string? Email, string? IdentityNumber, string? Address, int Stays);

public sealed record FrontDeskBookingDetail(
    FrontDeskBookingItem Summary,
    FrontDeskCustomer Customer,
    string? SpecialRequest,
    DateTime? HoldExpiresAt,
    IReadOnlyList<BookingRoomLine> Rooms,
    IReadOnlyList<BookingNightLine> Nights,
    IReadOnlyList<StayGuestDto> Guests,
    IReadOnlyList<DepositLine> Deposits,
    IReadOnlyList<InvoiceLine> Invoices,
    IReadOnlyList<CancellationLine> Cancellations,
    IReadOnlyList<StatusHistoryLine> History);

// ---------------- Tạo booking tại quầy ----------------
public sealed record NewCustomerRequest(
    [param: Required, StringLength(120)] string FullName,
    [param: StringLength(20)] string? Phone,
    [param: StringLength(150)] string? Email,
    [param: StringLength(40)] string? IdentityNumber,
    [param: StringLength(255)] string? Address);

public sealed record DeskDepositRequest(long PaymentMethodId, decimal Amount, string? Note = null, string? Reference = null);

public sealed record CreateDeskBookingRequest(
    long? CustomerId,
    NewCustomerRequest? NewCustomer,
    [param: Required, MinLength(1)] IReadOnlyList<long> RoomIds,
    DateTime CheckIn,
    DateTime CheckOut,
    int Adults = 1,
    int Children = 0,
    string Channel = "QUAY",
    string? SpecialRequest = null,
    DeskDepositRequest? Deposit = null);

// ---------------- Thay đổi booking ----------------
public sealed record ChangeDatesRequest(DateTime CheckIn, DateTime CheckOut, string? Reason);

public sealed record ChangeRoomRequest(long DetailId, long NewRoomId, string? Reason);

public sealed record DeskCancelRequest([param: Required, StringLength(1500)] string Reason);

public sealed record DeskNoteRequest(string? Note);

public sealed record DecideCancellationRequest(decimal ApprovedRefund, string? Note);

public sealed record ChangeResult(FrontDeskBookingDetail Booking, decimal OldTotal, decimal NewTotal, string Message);

// ---------------- Phòng trống ----------------
public sealed record AvailableRoom(
    long Id,
    string RoomNumber,
    string? Floor,
    string? View,
    long RoomTypeId,
    string RoomTypeName,
    int MaxAdults,
    int MaxChildren,
    decimal BaseNightlyPrice,
    decimal NightSurcharge,
    string Status);

// ---------------- Lễ tân trong ngày ----------------
public sealed record FrontDeskDay(
    DateOnly Date,
    IReadOnlyList<FrontDeskBookingItem> Arrivals,
    IReadOnlyList<FrontDeskBookingItem> Departures,
    IReadOnlyList<FrontDeskBookingItem> InHouse);

// ---------------- Nhận / trả phòng ----------------
public sealed record DeskCheckInRequest(IReadOnlyList<StayGuestDto>? Guests, bool AllowNotReadyRooms = false);

public sealed record CheckOutPreview(
    long BookingId,
    string BookingCode,
    DateTime PlannedCheckIn,
    DateTime PlannedCheckOut,
    DateTime? ActualCheckIn,
    DateTime Now,
    decimal RoomAmount,
    decimal EarlyCheckInSurcharge,
    decimal LateCheckOutSurcharge,
    decimal Compensation,
    decimal VatRate,
    decimal Deposit,
    decimal EstimatedTotal,
    decimal EstimatedDue,
    IReadOnlyList<string> Notes);

public sealed record DeskCheckOutRequest(decimal Surcharge, decimal VatRate = 0.1m);

public sealed record DeskCheckOutResult(InvoiceLine Invoice, IReadOnlyList<string> RoomsToClean, string Message);

public sealed record PaymentMethodItem(long Id, string Code, string Name);
