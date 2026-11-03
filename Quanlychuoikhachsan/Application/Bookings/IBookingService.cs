namespace Quanlychuoikhachsan.Application.Bookings;

public interface IBookingService
{
    Task<IReadOnlyList<RoomSummaryResponse>> FindAvailableRoomsAsync(RoomAvailabilityQuery query, CancellationToken cancellationToken);
    Task<PricePreviewResponse> PreviewPriceAsync(long roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken);
    Task<BookingResponse> CreateBookingAsync(long accountId, CreateBookingRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> UpdateBookingAsync(long accountId, long bookingId, PatchBookingRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> ConfirmDepositAsync(long accountId, long bookingId, ConfirmDepositRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> CheckInAsync(long bookingId, CheckInRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> CheckOutAsync(long bookingId, CheckOutRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> AddInvoicePaymentAsync(long invoiceId, AddInvoicePaymentRequest request, CancellationToken cancellationToken);
    Task<CancellationRequestResponse> CancelAsync(long accountId, long bookingId, CancelBookingRequest request, CancellationToken cancellationToken);
    Task<CancellationRequestResponse> ApproveCancellationAsync(long requestId, ApproveCancellationRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> MarkNoShowAsync(long bookingId, MarkNoShowRequest request, CancellationToken cancellationToken);
}
