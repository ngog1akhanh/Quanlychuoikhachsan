namespace Quanlychuoikhachsan.Application.FrontDesk;

public interface IFrontDeskService
{
    Task<FrontDeskBookingPage> SearchAsync(FrontDeskBookingQuery query, CancellationToken ct);
    Task<FrontDeskBookingDetail> GetDetailAsync(long bookingId, CancellationToken ct);
    Task<IReadOnlyList<AvailableRoom>> FindAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, long? excludeBookingId, CancellationToken ct);
    Task<IReadOnlyList<FrontDeskCustomer>> FindCustomersAsync(string? q, CancellationToken ct);
    Task<IReadOnlyList<PaymentMethodItem>> ListPaymentMethodsAsync(CancellationToken ct);
    Task<FrontDeskBookingDetail> CreateAsync(long accountId, CreateDeskBookingRequest request, CancellationToken ct);
    Task<FrontDeskBookingDetail> ConfirmDepositAsync(long accountId, long bookingId, DeskDepositRequest request, CancellationToken ct);
    Task<ChangeResult> ChangeDatesAsync(long accountId, long bookingId, ChangeDatesRequest request, CancellationToken ct);
    Task<ChangeResult> ChangeRoomAsync(long accountId, long bookingId, ChangeRoomRequest request, CancellationToken ct);
    Task<ChangeResult> CancelAsync(long accountId, long bookingId, DeskCancelRequest request, CancellationToken ct);
    Task<FrontDeskBookingDetail> MarkNoShowAsync(long accountId, long bookingId, DeskNoteRequest request, CancellationToken ct);
    Task<IReadOnlyList<CancellationRequestItem>> ListCancellationsAsync(string? status, CancellationToken ct);
    Task<CancellationRequestItem> ApproveCancellationAsync(long accountId, long requestId, DecideCancellationRequest request, CancellationToken ct);
    Task<CancellationRequestItem> RejectCancellationAsync(long accountId, long requestId, DeskNoteRequest request, CancellationToken ct);
    Task<FrontDeskDay> GetDayAsync(DateOnly date, CancellationToken ct);
    Task<FrontDeskBookingDetail> SaveGuestsAsync(long bookingId, IReadOnlyList<StayGuestDto> guests, CancellationToken ct);
    Task<FrontDeskBookingDetail> CheckInAsync(long accountId, long bookingId, DeskCheckInRequest request, CancellationToken ct);
    Task<CheckOutPreview> PreviewCheckOutAsync(long bookingId, CancellationToken ct);
    Task<DeskCheckOutResult> CheckOutAsync(long accountId, long bookingId, DeskCheckOutRequest request, CancellationToken ct);
}

public sealed record CancellationRequestItem(
    long Id, string Code, long BookingId, string BookingCode, string CustomerName, string Reason,
    DateTime RequestedAt, decimal ExpectedRefund, decimal ApprovedRefund, string Status, string? Note);

