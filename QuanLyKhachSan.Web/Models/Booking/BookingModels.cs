using System.ComponentModel.DataAnnotations;

namespace QuanLyKhachSan.Web.Models.Booking;

/// <summary>POST /v1/bookings</summary>
public sealed record CreateBookingRequest(
    IReadOnlyList<long> RoomIds,
    DateTime CheckIn,
    DateTime CheckOut,
    int Adults = 1,
    int Children = 0,
    string? SpecialRequest = null,
    [param: System.ComponentModel.DataAnnotations.StringLength(120)] string? ContactName = null,
    [param: System.ComponentModel.DataAnnotations.Phone, System.ComponentModel.DataAnnotations.StringLength(20)] string? ContactPhone = null,
    [param: System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(150)] string? ContactEmail = null);

public sealed record NightPrice(
    DateOnly StayDate,
    decimal BasePrice,
    decimal RoomSurcharge,
    long? PricingPolicyId,
    decimal Adjustment,
    decimal FinalPrice);

/// <summary>Kết quả tạo booking (giữ phòng) / xác nhận cọc.</summary>
public sealed record BookingResponse(
    string Name,
    long Id,
    string BookingCode,
    string Status,
    DateTime CheckIn,
    DateTime CheckOut,
    decimal TotalRoomAmount,
    DateTime? HoldExpiresAt,
    IReadOnlyList<NightPrice>? Nights);

/// <summary>Một dòng của GET /v1/me/bookings.</summary>
public sealed record BookingListItem(
    long Id,
    string Name,
    string BookingCode,
    string Status,
    DateTime CheckIn,
    DateTime CheckOut,
    decimal Total);

public sealed record PatchBookingRequest(string? SpecialRequest);

/// <summary>POST /v1/bookings/{id}:cancel</summary>
public sealed record CancelBookingRequest(string Reason);

public sealed record CancellationRequestResponse(
    long Id,
    string Name,
    long BookingId,
    string Status,
    decimal ExpectedRefund,
    decimal ApprovedRefund);

/// <summary>Form thông tin người đặt (GuestInformationForm).</summary>
public sealed class GuestInformation
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string PhoneNumber { get; set; } = "";

    [StringLength(1000, ErrorMessage = "Tối đa 1000 ký tự")]
    public string? SpecialRequest { get; set; }

    public bool AcceptPolicy { get; set; }
}

/// <summary>Trạng thái booking (khớp cột dat_phong.trang_thai).</summary>
public static class BookingStatus
{
    public const string Hold = "HOLD";
    public const string WaitingDeposit = "CHO_COC";
    public const string Booked = "BOOKED";
    public const string CheckedIn = "DANG_O";
    public const string CheckedOut = "DA_TRA";
    public const string Cancelled = "DA_HUY";
    public const string NoShow = "NO_SHOW";

    public static string Label(string? s) => s switch
    {
        Hold => "Đang giữ phòng",
        WaitingDeposit => "Chờ đặt cọc",
        Booked => "Đã xác nhận",
        CheckedIn => "Đang lưu trú",
        CheckedOut => "Đã trả phòng",
        Cancelled => "Đã hủy",
        NoShow => "Không đến",
        _ => s ?? ""
    };

    public static string CssClass(string? s) => s switch
    {
        Hold or WaitingDeposit => "badge-warn",
        Booked or CheckedIn => "badge-ok",
        CheckedOut => "badge-info",
        Cancelled or NoShow => "badge-danger",
        _ => "badge-info"
    };

    public static bool CanCancel(string? s) => s is Hold or WaitingDeposit or Booked;
    public static bool CanReview(string? s) => s == CheckedOut;
}
