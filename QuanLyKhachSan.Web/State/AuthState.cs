using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace QuanLyKhachSan.Web.State;

/// <summary>
/// Lưu trạng thái đăng nhập (JWT + tên). Lưu vào sessionStorage của trình duyệt (đã mã hóa)
/// để F5 không mất đăng nhập. Scoped: mỗi kết nối Blazor có 1 instance.
/// </summary>
public sealed class AuthState
{
    private const string StorageKey = "hotel.auth";
    private readonly ProtectedSessionStorage _storage;
    private Task? _init;

    private readonly BookingState? _booking;
    public AuthState(ProtectedSessionStorage storage, BookingState? booking = null) { _storage = storage; _booking = booking; }

    public string? Token { get; private set; }
    public string? CustomerName { get; private set; }
    public string? Email { get; private set; }
    public long? AccountId { get; private set; }
    public long? CustomerId { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(Token) && (ExpiresAtUtc is null || ExpiresAtUtc > DateTime.UtcNow);

    /// <summary>Bắn ra khi đăng nhập / đăng xuất để Header cập nhật.</summary>
    public event Action? Changed;

    /// <summary>Khôi phục đăng nhập từ sessionStorage (chỉ chạy 1 lần; gọi nhiều nơi vẫn an toàn).</summary>
    public Task EnsureInitializedAsync() => _init ??= LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var result = await _storage.GetAsync<StoredAuth>(StorageKey);
            if (result.Success && result.Value is { } v && !string.IsNullOrEmpty(v.Token))
                Apply(v.Token, v.CustomerName, v.ExpiresAtUtc, notify: true);
        }
        catch
        {
            // Dữ liệu hỏng / khóa mã hóa đổi => coi như chưa đăng nhập
        }
    }

    /// <summary>
    /// Dùng ở các trang cần đăng nhập: chờ khôi phục phiên, nếu chưa đăng nhập thì chuyển sang /login?returnUrl=...
    /// </summary>
    public async Task<bool> RequireLoginAsync(Microsoft.AspNetCore.Components.NavigationManager nav)
    {
        await EnsureInitializedAsync();
        if (IsAuthenticated) return true;
        var back = "/" + nav.ToBaseRelativePath(nav.Uri);
        nav.NavigateTo("login?returnUrl=" + Uri.EscapeDataString(back));
        return false;
    }

    public async Task SignInAsync(string token, string customerName, int expiresInSeconds)
    {
        var previousAccount = AccountId;
        var expires = DateTime.UtcNow.AddSeconds(expiresInSeconds > 0 ? expiresInSeconds : 3600);
        _booking?.ResetGuest();
        Apply(token, customerName, expires, notify: true);
        if (previousAccount is not null && previousAccount != AccountId) _booking?.ClearSelection();
        try { await _storage.SetAsync(StorageKey, new StoredAuth(token, customerName, expires)); } catch { }
    }

    public async Task SignOutAsync()
    {
        _booking?.ResetGuest();
        _booking?.ClearSelection();
        Token = null; CustomerName = null; Email = null; AccountId = null; CustomerId = null; ExpiresAtUtc = null;
        try { await _storage.DeleteAsync(StorageKey); } catch { }
        Changed?.Invoke();
    }

    public void UpdateName(string name)
    {
        CustomerName = name;
        Changed?.Invoke();
    }

    private void Apply(string token, string? name, DateTime? expires, bool notify)
    {
        Token = token;
        CustomerName = name;
        ExpiresAtUtc = expires;
        AccountId = null; CustomerId = null; Email = null;
        ReadClaims(token);
        if (notify) Changed?.Invoke();
    }

    /// <summary>Đọc các claim cơ bản từ JWT (không xác thực chữ ký – việc đó do API làm).</summary>
    private void ReadClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = doc.RootElement;

            string? Get(params string[] names)
            {
                foreach (var n in names)
                    if (root.TryGetProperty(n, out var p))
                        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
                return null;
            }

            if (long.TryParse(Get("sub", "nameid", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var acc))
                AccountId = acc;
            if (long.TryParse(Get("customer_id", "khach_hang_id"), out var cus))
                CustomerId = cus;
            Email = Get("email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        }
        catch
        {
            // token mock hoặc không phải JWT – bỏ qua
        }
    }

    private sealed record StoredAuth(string Token, string? CustomerName, DateTime? ExpiresAtUtc);
}
