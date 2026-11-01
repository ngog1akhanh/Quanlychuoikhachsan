using System.ComponentModel.DataAnnotations;

namespace QuanLyKhachSan.Web.Models.Customer;

public sealed record CustomerResponse(
    string Name,
    long Id,
    string CustomerCode,
    string FullName,
    string? Email,
    string? PhoneNumber,
    DateOnly? BirthDate,
    string? Gender,
    string? IdentityNumber,
    string? Address,
    DateTime CreateTime,
    DateTime UpdateTime);

/// <summary>PUT /v1/me – form cập nhật hồ sơ.</summary>
public sealed class UpdateCustomerRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(120)]
    public string? FullName { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(150)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ"), StringLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? IdentityNumber { get; set; }
    public string? Address { get; set; }
}

/// <summary>Form đổi mật khẩu (POST /v1/me/change-password).</summary>
public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu mới"), MinLength(8, ErrorMessage = "Tối thiểu 8 ký tự")]
    public string NewPassword { get; set; } = "";

    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    public string ConfirmPassword { get; set; } = "";
}
