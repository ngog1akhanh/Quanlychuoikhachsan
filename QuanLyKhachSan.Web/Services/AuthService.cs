using QuanLyKhachSan.Web.Models.Auth;
using QuanLyKhachSan.Web.Models.Customer;
using QuanLyKhachSan.Web.State;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Đăng ký, đăng nhập, đăng xuất, quên mật khẩu.</summary>
public sealed class AuthService
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;

    public AuthService(ApiClient api, AuthState auth)
    {
        _api = api;
        _auth = auth;
    }

    /// <summary>POST /v1/auth/login</summary>
    public async Task LoginAsync(LoginRequest request)
    {
        AuthTokenResponse token;
        if (_api.UseMock)
        {
            await Task.Delay(400);
            if (request.Password.Length < 6)
                throw new ApiException("Sai thông tin đăng nhập (mock: mật khẩu tối thiểu 6 ký tự).");
            token = new AuthTokenResponse("mock-token", "Bearer", 3600, MockData.Customer.FullName);
        }
        else
        {
            token = await _api.PostAsync<AuthTokenResponse>("v1/auth/login",
                new { identifier = request.Identifier.Trim(), password = request.Password });
        }

        await _auth.SignInAsync(token.AccessToken, token.CustomerName, token.ExpiresIn);
    }

    /// <summary>POST /v1/auth/register (trả 201, envelope data = CustomerResponse). Không tự đăng nhập.</summary>
    public async Task RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ApiException("Nhập email hoặc số điện thoại.");

        if (_api.UseMock)
        {
            await Task.Delay(400);
            return;
        }

        await _api.PostAsync<ApiEnvelope<CustomerResponse>>("v1/auth/register", new
        {
            full_name = request.FullName.Trim(),
            email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            phone_number = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            password = request.Password
        });
    }

    /// <summary>POST /v1/auth/forgot-password { email } – API trả 202 và gửi email (cần cấu hình SMTP ở backend).</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (_api.UseMock)
        {
            await Task.Delay(400);
            return;
        }
        await _api.PostAsync<object>("v1/auth/forgot-password", new { email = request.Email.Trim() });
    }

    public Task LogoutAsync() => _auth.SignOutAsync();
}
