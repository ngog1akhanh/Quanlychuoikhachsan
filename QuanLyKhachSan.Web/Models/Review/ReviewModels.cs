using System.ComponentModel.DataAnnotations;

namespace QuanLyKhachSan.Web.Models.Review;

/// <summary>Form đánh giá → POST /v1/reviews (overall_score gửi lên là "rating").</summary>
public sealed class CreateReviewRequest
{
    public long BookingId { get; set; }

    [Range(1, 5, ErrorMessage = "Chọn từ 1 đến 5 sao")]
    public int OverallScore { get; set; } = 5;

    [Range(1, 5)] public int? RoomScore { get; set; } = 5;
    [Range(1, 5)] public int? ServiceScore { get; set; } = 5;

    [StringLength(3000, ErrorMessage = "Tối đa 3000 ký tự")]
    public string? Content { get; set; }
}

public sealed record ReviewResponse(
    long Id,
    long BookingId,
    int OverallScore,
    int? RoomScore,
    int? ServiceScore,
    string? Content,
    DateTime CreatedAt,
    string? HotelReply);
