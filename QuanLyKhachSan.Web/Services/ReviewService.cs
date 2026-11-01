using QuanLyKhachSan.Web.Models.Review;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Đánh giá sau lưu trú (chỉ booking đã trả phòng, mỗi booking một đánh giá).</summary>
public sealed class ReviewService
{
    private readonly ApiClient _api;

    public ReviewService(ApiClient api) => _api = api;

    // API trả thẳng bản ghi danh_gia nên tên trường là tiếng Việt (snake_case).
    private sealed record ReviewRow(long Id, long DatPhongId, byte DiemTong, byte? DiemPhong, byte? DiemDichVu,
        string? NoiDung, DateTime NgayDanhGia, string? PhanHoiKhachSan);
    private sealed record ReviewPage(List<ReviewRow>? Items);

    private static ReviewResponse Map(ReviewRow r) => new(
        r.Id, r.DatPhongId, (int)r.DiemTong, (int?)r.DiemPhong, (int?)r.DiemDichVu,
        r.NoiDung, r.NgayDanhGia, r.PhanHoiKhachSan);

    /// <summary>POST /v1/reviews</summary>
    public async Task<ReviewResponse> CreateAsync(CreateReviewRequest r)
    {
        if (_api.UseMock)
        {
            await Task.Delay(400);
            lock (MockData.Reviews)
            {
                if (MockData.Reviews.Any(x => x.BookingId == r.BookingId))
                    throw new ApiException("Mỗi booking chỉ được đánh giá một lần.");
                var review = new ReviewResponse(MockData.Reviews.Count + 1, r.BookingId, r.OverallScore,
                    r.RoomScore, r.ServiceScore, r.Content, DateTime.UtcNow, null);
                MockData.Reviews.Add(review);
                return review;
            }
        }

        var row = await _api.PostAsync<ReviewRow>("v1/reviews", new
        {
            booking_id = r.BookingId,
            rating = r.OverallScore,
            room_rating = r.RoomScore,
            service_rating = r.ServiceScore,
            content = r.Content
        });
        return Map(row);
    }

    /// <summary>GET /v1/reviews/mine – tìm đánh giá của booking (kèm phản hồi của khách sạn).</summary>
    public async Task<ReviewResponse?> GetByBookingAsync(long bookingId)
    {
        if (_api.UseMock)
        {
            await Task.Delay(150);
            lock (MockData.Reviews) return MockData.Reviews.FirstOrDefault(x => x.BookingId == bookingId);
        }
        var page = await _api.GetAsync<ReviewPage>("v1/reviews/mine?page=1&pageSize=100");
        var row = page?.Items?.FirstOrDefault(x => x.DatPhongId == bookingId);
        return row is null ? null : Map(row);
    }
}
