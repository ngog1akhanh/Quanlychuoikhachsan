using System.Net.Http.Json;
using HotelAdmin.Models;

namespace HotelAdmin.Services;

/// <summary>Các hàm gọi API của Giai đoạn 1: phòng, đặt phòng tại quầy, nhận/trả phòng.</summary>
public sealed partial class ApiClient
{
    // ---------------- Phòng (RoomAdminController + RoomsController/RoomTypesController) ----------------
    public Task<List<RoomBoardItem>> GetRoomBoardAsync(CancellationToken ct = default)
        => SendJsonAsync<List<RoomBoardItem>>(HttpMethod.Get, "v1/room-board", null, ct);

    public Task<List<RoomTypeSummary>> GetRoomTypeSummariesAsync(CancellationToken ct = default)
        => SendJsonAsync<List<RoomTypeSummary>>(HttpMethod.Get, "v1/room-board/types", null, ct);

    public Task<RoomCalendar> GetRoomCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => SendJsonAsync<RoomCalendar>(HttpMethod.Get, $"v1/room-calendar?from={D(from)}&to={D(to)}", null, ct);

    public Task<RoomBoardItem> UpdateRoomAsync(long roomId, UpdateRoomRequest request, CancellationToken ct = default)
        => SendJsonAsync<RoomBoardItem>(HttpMethod.Patch, $"v1/rooms/{roomId}", request, ct);

    public Task<RoomBoardItem> SetRoomStatusAsync(long roomId, string status, string? note, CancellationToken ct = default)
        => SendJsonAsync<RoomBoardItem>(HttpMethod.Post, $"v1/rooms/{roomId}:set-status", new SetRoomStatusRequest(status, note), ct);

    public Task<RoomTypeSummary> UpdateRoomTypeAsync(long roomTypeId, UpdateRoomTypeRequest request, CancellationToken ct = default)
        => SendJsonAsync<RoomTypeSummary>(HttpMethod.Patch, $"v1/room-types/{roomTypeId}", request, ct);

    public async Task CreateRoomTypeAsync(CreateRoomTypeRequest request, CancellationToken ct = default)
        => await SendJsonAsync<ApiEnvelope<RoomTypeSummaryLite>>(HttpMethod.Post, "v1/room-types", request, ct);

    public async Task CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
        => await SendJsonAsync<ApiEnvelope<RoomResponse>>(HttpMethod.Post, "v1/rooms", request, ct);

    public Task<PricePreview> PreviewPriceAsync(long roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
        => SendJsonAsync<PricePreview>(HttpMethod.Get, $"v1/rooms/{roomId}/price?checkInDate={D(checkIn)}&checkOutDate={D(checkOut)}", null, ct);

    // ---------------- Đặt phòng (FrontDeskController) ----------------
    public Task<FrontDeskBookingPage> SearchBookingsAsync(string? q, string? status, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var url = $"v1/front-desk/bookings?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q.Trim())}";
        if (!string.IsNullOrWhiteSpace(status)) url += $"&status={Uri.EscapeDataString(status)}";
        if (from is { } f) url += $"&from={D(f)}";
        if (to is { } t) url += $"&to={D(t)}";
        return SendJsonAsync<FrontDeskBookingPage>(HttpMethod.Get, url, null, ct);
    }

    public Task<FrontDeskBookingDetail> GetBookingDetailAsync(long bookingId, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Get, $"v1/front-desk/bookings/{bookingId}", null, ct);

    public Task<List<AvailableRoom>> GetAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, long? excludeBookingId = null, CancellationToken ct = default)
    {
        var url = $"v1/front-desk/available-rooms?checkIn={D(checkIn)}&checkOut={D(checkOut)}";
        if (excludeBookingId is { } id) url += $"&excludeBookingId={id}";
        return SendJsonAsync<List<AvailableRoom>>(HttpMethod.Get, url, null, ct);
    }

    public Task<List<FrontDeskCustomer>> FindCustomersAsync(string? q, CancellationToken ct = default)
        => SendJsonAsync<List<FrontDeskCustomer>>(HttpMethod.Get, $"v1/front-desk/customers?q={Uri.EscapeDataString(q ?? "")}", null, ct);

    public Task<List<PaymentMethodItem>> GetPaymentMethodsAsync(CancellationToken ct = default)
        => SendJsonAsync<List<PaymentMethodItem>>(HttpMethod.Get, "v1/front-desk/payment-methods", null, ct);

    public Task<FrontDeskBookingDetail> CreateDeskBookingAsync(CreateDeskBookingRequest request, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Post, "v1/front-desk/bookings", request, ct);

    public Task<FrontDeskBookingDetail> ConfirmDepositAsync(long bookingId, DeskDepositRequest request, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:confirm-deposit", request, ct);

    public Task<ChangeResult> ChangeDatesAsync(long bookingId, ChangeDatesRequest request, CancellationToken ct = default)
        => SendJsonAsync<ChangeResult>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:change-dates", request, ct);

    public Task<ChangeResult> ChangeRoomAsync(long bookingId, ChangeRoomRequest request, CancellationToken ct = default)
        => SendJsonAsync<ChangeResult>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:change-room", request, ct);

    public Task<ChangeResult> CancelBookingAsync(long bookingId, string reason, CancellationToken ct = default)
        => SendJsonAsync<ChangeResult>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:cancel", new DeskCancelRequest(reason), ct);

    public Task<FrontDeskBookingDetail> MarkNoShowAsync(long bookingId, string? note, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:mark-no-show", new DeskNoteRequest(note), ct);

    public Task<List<CancellationRequestItem>> GetCancellationRequestsAsync(string? status, CancellationToken ct = default)
        => SendJsonAsync<List<CancellationRequestItem>>(HttpMethod.Get,
            "v1/front-desk/cancellation-requests" + (string.IsNullOrEmpty(status) ? "" : $"?status={status}"), null, ct);

    public Task<CancellationRequestItem> ApproveCancellationAsync(long requestId, decimal refund, string? note, CancellationToken ct = default)
        => SendJsonAsync<CancellationRequestItem>(HttpMethod.Post, $"v1/front-desk/cancellation-requests/{requestId}:approve",
            new DecideCancellationRequest(refund, note), ct);

    public Task<CancellationRequestItem> RejectCancellationAsync(long requestId, string? note, CancellationToken ct = default)
        => SendJsonAsync<CancellationRequestItem>(HttpMethod.Post, $"v1/front-desk/cancellation-requests/{requestId}:reject",
            new DeskNoteRequest(note), ct);

    // ---------------- Lễ tân ----------------
    public Task<FrontDeskDay> GetFrontDeskDayAsync(DateOnly date, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskDay>(HttpMethod.Get, $"v1/front-desk/day?date={D(date)}", null, ct);

    public Task<FrontDeskBookingDetail> SaveGuestsAsync(long bookingId, List<StayGuestDto> guests, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Put, $"v1/front-desk/bookings/{bookingId}/guests", guests, ct);

    public Task<FrontDeskBookingDetail> CheckInAsync(long bookingId, DeskCheckInRequest request, CancellationToken ct = default)
        => SendJsonAsync<FrontDeskBookingDetail>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:check-in", request, ct);

    public Task<CheckOutPreview> GetCheckOutPreviewAsync(long bookingId, CancellationToken ct = default)
        => SendJsonAsync<CheckOutPreview>(HttpMethod.Get, $"v1/front-desk/bookings/{bookingId}/checkout-preview", null, ct);

    public Task<DeskCheckOutResult> CheckOutAsync(long bookingId, DeskCheckOutRequest request, CancellationToken ct = default)
        => SendJsonAsync<DeskCheckOutResult>(HttpMethod.Post, $"v1/front-desk/bookings/{bookingId}:check-out", request, ct);

    // Chốt và thu tiền riêng biệt để nhân viên xác nhận đúng số tiền đã nhận.
    public Task<RoomInvoice> PrepareInvoiceAsync(long bookingId, decimal additionalSurcharge, decimal vatRate, CancellationToken ct = default)
        => SendJsonAsync<RoomInvoice>(HttpMethod.Post, $"v1/bookings/{bookingId}/invoice:prepare",
            new { employee_id = 0, early_or_late_surcharge = additionalSurcharge, vat_rate = vatRate }, ct);

    public Task<RoomInvoice> PayInvoiceAsync(long invoiceId, InvoicePayment request, CancellationToken ct = default)
        => SendJsonAsync<RoomInvoice>(HttpMethod.Post, $"v1/invoices/{invoiceId}:pay", request, ct);

    // ---------------- Hạ tầng: gửi JSON với mọi phương thức HTTP ----------------
    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var response = await SendSafeAsync(async () =>
        {
            // Tạo request mới mỗi lần gửi (cần khi phải gửi lại sau redirect).
            using var request = new HttpRequestMessage(method, new Uri(baseUri, url));
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), null, Json);
            return await http.SendAsync(request, ct);
        });
        return await ReadAsync<T>(response, ct);
    }
}

/// <summary>Kết quả tối thiểu của POST /v1/room-types (chỉ cần biết đã tạo).</summary>
public sealed record RoomTypeSummaryLite(long Id, string Code, string Name);
