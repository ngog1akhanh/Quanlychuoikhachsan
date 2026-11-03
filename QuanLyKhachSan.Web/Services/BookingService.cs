using QuanLyKhachSan.Web.Models.Booking;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Đặt phòng, xem booking của tôi, yêu cầu hủy.</summary>
public sealed class BookingService
{
    private readonly ApiClient _api;

    public BookingService(ApiClient api) => _api = api;

    /// <summary>POST /v1/bookings – tạo booking và giữ phòng (HOLD 15 phút). Response: { data: BookingResponse }.</summary>
    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request)
    {
        if (_api.UseMock)
        {
            await Task.Delay(500);
            var rooms = MockData.Rooms.Where(r => request.RoomIds.Contains(r.Id)).ToList();
            var b = MockData.MakeBooking(rooms, request.CheckIn, request.CheckOut, BookingStatus.Hold);
            lock (MockData.Bookings) MockData.Bookings.Add(b);
            return b;
        }
        return await _api.PostEnvelopeAsync<BookingResponse>("v1/bookings", request);
    }

    private sealed record MyBookingRow(long Id, string Code, string Status, DateTime CheckIn, DateTime CheckOut, decimal Total);
    private sealed record MyBookingPage(List<MyBookingRow>? Items);

    /// <summary>GET /v1/me/bookings?page&amp;pageSize → { items, total, page, page_size }</summary>
    public async Task<List<BookingListItem>> GetMyBookingsAsync()
    {
        if (_api.UseMock)
        {
            await Task.Delay(300);
            lock (MockData.Bookings)
                return MockData.Bookings.OrderByDescending(b => b.Id).Select(MockData.ToListItem).ToList();
        }
        var page = await _api.GetAsync<MyBookingPage>("v1/me/bookings?page=1&pageSize=100");
        return (page?.Items ?? new List<MyBookingRow>())
            .Select(b => new BookingListItem(b.Id, $"bookings/{b.Id}", b.Code, b.Status, b.CheckIn, b.CheckOut, b.Total))
            .ToList();
    }

    /// <summary>
    /// Lấy một booking của tôi từ danh sách /v1/me/bookings.
    /// </summary>
    public async Task<BookingListItem?> GetMyBookingAsync(long id)
    {
        var all = await GetMyBookingsAsync();
        return all.FirstOrDefault(b => b.Id == id);
    }

    /// <summary>Chỉ có ở chế độ mock – trả chi tiết từng đêm. Thật: null (dùng BookingState.CurrentBooking).</summary>
    public BookingResponse? GetMockDetail(long id)
    {
        if (!_api.UseMock) return null;
        lock (MockData.Bookings) return MockData.Bookings.FirstOrDefault(b => b.Id == id);
    }

    /// <summary>PATCH /v1/bookings/{id} – chỉ sửa yêu cầu đặc biệt khi đang HOLD.</summary>
    public async Task UpdateSpecialRequestAsync(long id, string? text)
    {
        if (_api.UseMock) { await Task.Delay(300); return; }
        await _api.PatchAsync<BookingResponse>($"v1/bookings/{id}", new PatchBookingRequest(text));
    }

    /// <summary>POST /v1/bookings/{id}:cancel – gửi yêu cầu hủy (quản lý duyệt sau).</summary>
    public async Task<CancellationRequestResponse> RequestCancelAsync(long id, string reason)
    {
        if (_api.UseMock)
        {
            await Task.Delay(400);
            BookingResponse? b;
            lock (MockData.Bookings) b = MockData.Bookings.FirstOrDefault(x => x.Id == id);
            var refund = b?.Status == BookingStatus.Booked ? Math.Round(b.TotalRoomAmount * 0.3m * 0.5m, 0) : 0;
            return new CancellationRequestResponse(1, "cancellation-requests/1", id, "CHO_DUYET", refund, 0);
        }
        return await _api.PostAsync<CancellationRequestResponse>($"v1/bookings/{id}:cancel", new CancelBookingRequest(reason));
    }
}
