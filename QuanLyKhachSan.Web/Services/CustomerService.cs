using QuanLyKhachSan.Web.Models.Customer;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Hồ sơ khách hàng (của chính người đăng nhập).</summary>
public sealed class CustomerService
{
    private readonly ApiClient _api;

    public CustomerService(ApiClient api) => _api = api;

    /// <summary>GET /v1/me – các trường hồ sơ khách nằm ở cấp gốc của response.</summary>
    public async Task<CustomerResponse> GetProfileAsync()
    {
        if (_api.UseMock) { await Task.Delay(250); return MockData.Customer; }
        return await _api.GetAsync<CustomerResponse>("v1/me");
    }

    /// <summary>PUT /v1/me. Email gắn với tài khoản đăng nhập nên không đổi được ở đây.</summary>
    public async Task<CustomerResponse> UpdateProfileAsync(long customerId, UpdateCustomerRequest r)
    {
        if (_api.UseMock)
        {
            await Task.Delay(350);
            MockData.Customer = MockData.Customer with
            {
                FullName = r.FullName ?? MockData.Customer.FullName,
                Email = r.Email, PhoneNumber = r.PhoneNumber, BirthDate = r.BirthDate,
                Gender = r.Gender, IdentityNumber = r.IdentityNumber, Address = r.Address,
                UpdateTime = DateTime.UtcNow
            };
            return MockData.Customer;
        }

        return await _api.PutAsync<CustomerResponse>("v1/me", new
        {
            full_name = r.FullName,
            birth_date = r.BirthDate,
            gender = r.Gender,
            identity_number = r.IdentityNumber,
            address = r.Address,
            phone_number = r.PhoneNumber
        });
    }

    /// <summary>POST /v1/me/change-password (trả 204).</summary>
    public async Task ChangePasswordAsync(ChangePasswordRequest r)
    {
        if (_api.UseMock) { await Task.Delay(350); return; }
        await _api.PostAsync<object>("v1/me/change-password", new
        {
            current_password = r.CurrentPassword,
            new_password = r.NewPassword
        });
    }
}
