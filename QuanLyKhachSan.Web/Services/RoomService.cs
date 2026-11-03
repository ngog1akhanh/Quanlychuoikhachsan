using QuanLyKhachSan.Web.Models.Room;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Tìm phòng trống (khách vãng lai dùng được).</summary>
public sealed class RoomService
{
    private readonly ApiClient _api;

    public RoomService(ApiClient api) => _api = api;

    private sealed record CatalogType(long Id, string MaLoaiPhong, string TenLoaiPhong, int SoNguoiLonToiDa,
        int SoTreEmToiDa, decimal? DienTichM2, decimal GiaCoBanDem, string? MoTa);

    // Trang chủ hiển thị loại phòng và giá từ DB, cùng mã lọc với trang tìm phòng.
    public async Task<List<PublicRoom>> GetHighlightsAsync()
    {
        if (_api.UseMock) return MockData.Rooms.DistinctBy(x => x.RoomTypeCode).Take(4).ToList();
        var types = await _api.GetAsync<List<CatalogType>>("v1/public/room-types");
        return types.OrderBy(x => x.GiaCoBanDem).Take(4).Select(x => new PublicRoom(x.Id, "", x.MaLoaiPhong,
            x.TenLoaiPhong, x.GiaCoBanDem, 0, null, null, x.DienTichM2, x.MoTa, x.SoNguoiLonToiDa, x.SoTreEmToiDa)).ToList();
    }

    // ---- Dạng dữ liệu API trả về (JSON snake_case) ----
    private sealed record AvailabilityItem(RoomSummary Room, PriceQuote Quote);
    private sealed record RoomSummary(long Id, string RoomCode, string RoomNumber, string RoomTypeCode, string RoomTypeName, decimal BaseNightlyPrice);
    private sealed record NightItem(DateOnly StayDate, decimal FinalPrice);
    private sealed record PriceQuote(long RoomId, List<NightItem>? Nights, decimal Total);
    private sealed record RoomTypeInfo(int SoNguoiLonToiDa, int SoTreEmToiDa, decimal? DienTichM2);
    private sealed record RoomDetail(long Id, string? Tang, string? LoaiView, string? MoTa, RoomTypeInfo? RoomType);

    /// <summary>
    /// GET /v1/public/rooms/availability → danh sách { room, quote }.
    /// Mỗi phòng gọi thêm GET /v1/public/rooms/{id} để lấy tầng, view, diện tích, mô tả.
    /// Giá 1 đêm hiển thị = tổng tiền báo giá / số đêm (đã gồm chính sách giá và phụ thu).
    /// </summary>
    public async Task<List<PublicRoom>> SearchAvailableAsync(RoomSearchQuery q, CancellationToken ct = default)
    {
        if (_api.UseMock)
        {
            await Task.Delay(350, ct);
            return MockData.Search(q);
        }

        var url = $"v1/public/rooms/availability?checkInDate={q.CheckIn:yyyy-MM-dd}&checkOutDate={q.CheckOut:yyyy-MM-dd}" +
                  $"&adults={q.Adults}&children={q.Children}";
        var items = await _api.GetAsync<List<AvailabilityItem>>(url, ct) ?? new List<AvailabilityItem>();

        using var gate = new SemaphoreSlim(6);
        var tasks = items.Select(async it =>
        {
            RoomDetail? d = null;
            await gate.WaitAsync(ct);
            try { d = await _api.GetAsync<RoomDetail>($"v1/public/rooms/{it.Room.Id}", ct); }
            catch (ApiException) { /* thiếu chi tiết thì vẫn hiển thị phòng */ }
            finally { gate.Release(); }

            var nights = Math.Max(1, it.Quote.Nights?.Count ?? q.Nights);
            var nightly = it.Quote.Total / nights;
            return new PublicRoom(
                it.Room.Id, it.Room.RoomNumber, it.Room.RoomTypeCode, it.Room.RoomTypeName,
                nightly, 0m, d?.Tang, d?.LoaiView, d?.RoomType?.DienTichM2, d?.MoTa,
                d?.RoomType?.SoNguoiLonToiDa ?? q.Adults, d?.RoomType?.SoTreEmToiDa ?? q.Children,
                it.Quote.Nights?.Select(n => new QuotedNight(n.StayDate, n.FinalPrice)).ToList());
        }).ToList();

        var rooms = await Task.WhenAll(tasks);
        return rooms.OrderBy(r => r.UnitPrice).ToList();
    }
}
