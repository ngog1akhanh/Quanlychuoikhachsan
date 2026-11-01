using System.ComponentModel.DataAnnotations;

namespace QuanLyKhachSan.Web.Models.Auth;

/// <summary>POST /v1/auth/login</summary>
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại")]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = "";
}

/// <summary>Kết quả đăng nhập: JWT + tên khách hàng.</summary>
public sealed record AuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string CustomerName);

/// <summary>POST /v1/auth/register</summary>
public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(120)]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(150)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ"), StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự"), StringLength(128)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [System.Text.Json.Serialization.JsonIgnore]
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Chưa có API – dự kiến POST /v1/auth/forgot-password</summary>
public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";
}
