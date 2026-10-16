using System.ComponentModel.DataAnnotations;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed record RoomAvailabilityQuery(DateOnly CheckInDate, DateOnly CheckOutDate, int Adults = 1, int Children = 0);

public sealed record RoomSummaryResponse(long Id, string RoomCode, string RoomNumber, string RoomTypeCode, string RoomTypeName, decimal BaseNightlyPrice);

public sealed record RoomTypeResponse(long Id, string Code, string Name, int MaxAdults, int MaxChildren, decimal BaseNightlyPrice, string Status);

public sealed record CreateRoomTypeRequest(
    [param: Required, StringLength(30)] string Code,
    [param: Required, StringLength(100)] string Name,
    int MaxAdults = 2,
    int MaxChildren = 0,
    decimal BaseNightlyPrice = 0,
    string? Description = null);

public sealed record RoomResponse(long Id, string Name, string RoomCode, string RoomNumber, long RoomTypeId, string RoomTypeCode, string Status);

public sealed record CreateRoomRequest(
    [param: Required, StringLength(30)] string RoomCode,
    [param: Required, StringLength(20)] string RoomNumber,
    long RoomTypeId,
    string? Floor = null,
    string? ViewType = null,
    decimal DefaultNightSurcharge = 0);

public sealed record NightPriceResponse(DateOnly StayDate, decimal BasePrice, decimal RoomSurcharge, long? PricingPolicyId, decimal Adjustment, decimal FinalPrice);

public sealed record PricePreviewResponse(long RoomId, DateOnly CheckInDate, DateOnly CheckOutDate, IReadOnlyList<NightPriceResponse> Nights, decimal Total);

public sealed record CreateBookingRequest(
    [param: Required, MinLength(1)] IReadOnlyList<long> RoomIds,
    DateTime CheckIn,
    DateTime CheckOut,
    int Adults = 1,
    int Children = 0,
    string? SpecialRequest = null);

public sealed record PatchBookingRequest(string? SpecialRequest);

public sealed record BookingResponse(
    string Name,
    long Id,
    string BookingCode,
    string Status,
    DateTime CheckIn,
    DateTime CheckOut,
    decimal TotalRoomAmount,
    DateTime? HoldExpiresAt,
    IReadOnlyList<NightPriceResponse> Nights);

public sealed record ConfirmDepositRequest(
    long PaymentMethodId,
    decimal Amount,
    [param: Required] string PaymentGateway,
    [param: Required] string TransactionId);

// Frontend sends these fields in snake_case; query metadata remains camelCase.
public sealed record CheckInRequest(long EmployeeId);
public sealed record CheckOutRequest(long EmployeeId, decimal EarlyOrLateSurcharge = 0, decimal VatRate = 0.1m);
public sealed record CancelBookingRequest(string Reason);
public sealed record ApproveCancellationRequest(long EmployeeId, decimal ApprovedRefund, string? Note = null);
public sealed record MarkNoShowRequest(long EmployeeId, string? Note = null);
public sealed record AddInvoicePaymentRequest(long PaymentMethodId, decimal Amount, long? EmployeeId, string? PaymentGateway, string? TransactionId);
public sealed record InvoiceResponse(long Id, string Name, long BookingId, decimal RoomAmount, decimal Surcharge, decimal Compensation, decimal Vat, decimal Total, decimal DepositApplied, decimal Paid, decimal Due, string Status);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextPageToken);
public sealed record PageQuery(int PageSize = 20, string? PageToken = null);
public sealed record BookingListItem(long Id, string Name, string BookingCode, string Status, DateTime CheckIn, DateTime CheckOut, decimal Total);
public sealed record TransactionListItem(long Id, string Name, long InvoiceId, decimal Amount, long PaymentMethodId, DateTime PaidAt, string Status);
public sealed record CancellationRequestResponse(long Id, string Name, long BookingId, string Status, decimal ExpectedRefund, decimal ApprovedRefund);
