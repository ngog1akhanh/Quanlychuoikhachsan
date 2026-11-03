using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HotelAdmin.Models;

namespace HotelAdmin.Services;

/// <summary>Lỗi trả về từ API (Google-style error hoặc payment envelope).</summary>
public sealed class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}

/// <summary>
/// Client gọi REST API /v1. JSON body/response dùng snake_case, query dùng camelCase (theo README backend).
/// Mọi kiểm tra quyền thật sự nằm ở backend; app chỉ ẩn/hiện chức năng cho tiện.
/// </summary>
public sealed partial class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient http;
    private Uri baseUri; // HttpClient.BaseAddress không đổi được sau request đầu, nên tự giữ địa chỉ gốc

    /// <summary>Phát sinh khi token hết hạn (401) để form chính quay về màn hình đăng nhập.</summary>
    public event EventHandler? SessionExpired;

    public ApiClient(AppConfig config)
    {
        // Không tự follow redirect: khi backend bật UseHttpsRedirection (http -> https),
        // HttpClient sẽ bỏ header Authorization ở request sau redirect -> luôn 401.
        // Ta tự chuyển BaseAddress sang địa chỉ mới rồi gửi lại (giữ nguyên token).
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            // Chỉ chấp nhận chứng chỉ dev tự ký khi gọi localhost.
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None || request.RequestUri?.IsLoopback == true
        };
        baseUri = new Uri(config.ApiBaseUrl);
        http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, config.RequestTimeoutSeconds))
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // Cho phép dùng cùng client trong kiểm thử HTTP, không phụ thuộc cửa sổ WinForms.
    public ApiClient(HttpClient client, Uri server)
    {
        http = client;
        baseUri = server;
    }

    public string BaseUrl => baseUri.ToString();

    public void SetToken(string token) =>
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void ClearToken() => http.DefaultRequestHeaders.Authorization = null;

    // ---------------- Tài khoản ----------------
    public Task<AuthTokenResponse> LoginAsync(string identifier, string password, CancellationToken ct = default)
        => PostAsync<AuthTokenResponse>("v1/auth/login", new LoginRequest(identifier, password), ct);

    public Task<MeResponse> GetMeAsync(CancellationToken ct = default) => GetAsync<MeResponse>("v1/me", ct);

    // ---------------- Báo cáo ----------------
    public Task<DashboardResponse> GetDashboardAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => GetAsync<DashboardResponse>($"v1/reports/dashboard?from={D(from)}&to={D(to)}", ct);

    public Task<RevenueReportResponse> GetRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => GetAsync<RevenueReportResponse>($"v1/reports/revenue?from={D(from)}&to={D(to)}", ct);

    public Task<OccupancyReportResponse> GetOccupancyAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => GetAsync<OccupancyReportResponse>($"v1/reports/occupancy?from={D(from)}&to={D(to)}", ct);

    // ---------------- Phòng ----------------
    public Task<List<RoomResponse>> GetRoomsAsync(CancellationToken ct = default)
        => GetAsync<List<RoomResponse>>("v1/rooms", ct);

    // ---------------- Đặt phòng ----------------
    public Task<PageResponse<BookingListItem>> GetBookingsPageAsync(int pageSize, string? pageToken, CancellationToken ct = default)
    {
        var url = $"v1/bookings?pageSize={pageSize}";
        if (!string.IsNullOrEmpty(pageToken)) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        return GetAsync<PageResponse<BookingListItem>>(url, ct);
    }

    /// <summary>Đọc lần lượt các trang booking (tối đa maxPages trang x 100 dòng).</summary>
    public async Task<List<BookingListItem>> GetAllBookingsAsync(int maxPages = 20, CancellationToken ct = default)
    {
        var result = new List<BookingListItem>();
        string? token = null;
        for (var i = 0; i < maxPages; i++)
        {
            var page = await GetBookingsPageAsync(100, token, ct);
            result.AddRange(page.Items);
            token = page.NextPageToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return result;
    }

    // ---------------- Hạ tầng gọi HTTP ----------------
    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd");

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await SendSafeAsync(() => http.GetAsync(new Uri(baseUri, url), ct));
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        using var response = await SendSafeAsync(() => http.PostAsJsonAsync(new Uri(baseUri, url), body, Json, ct));
        return await ReadAsync<T>(response, ct);
    }

    private async Task<HttpResponseMessage> SendSafeAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            var response = await send();
            // Gặp redirect (vd 307 http://localhost:5005 -> https://localhost:7147): đổi máy chủ gốc rồi gửi lại 1 lần.
            if ((int)response.StatusCode is 301 or 302 or 307 or 308 && response.Headers.Location is { IsAbsoluteUri: true } location
                && location.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
                && baseUri.Scheme == Uri.UriSchemeHttp && location.Scheme == Uri.UriSchemeHttps)
            {
                response.Dispose();
                baseUri = new Uri(location.GetLeftPart(UriPartial.Authority) + "/");
                response = await send();
            }
            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable,
                $"Không kết nối được máy chủ API. Hãy kiểm tra backend đã chạy chưa.\n({ex.Message})");
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(HttpStatusCode.RequestTimeout, "Máy chủ phản hồi quá lâu, vui lòng thử lại.");
        }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            return value ?? throw new ApiException(response.StatusCode, "Phản hồi rỗng từ máy chủ.");
        }

        var message = await ReadErrorMessageAsync(response, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && http.DefaultRequestHeaders.Authorization is not null)
            SessionExpired?.Invoke(this, EventArgs.Empty);
        throw new ApiException(response.StatusCode, message);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu (hoặc API chưa được triển khai).",
            _ => $"Lỗi máy chủ ({(int)response.StatusCode})."
        };
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m1))
                return m1.GetString() ?? fallback;
            if (root.TryGetProperty("message", out var m2))
                return m2.GetString() ?? fallback;
        }
        catch (JsonException)
        {
            // Body không phải JSON -> dùng thông báo mặc định.
        }
        return fallback;
    }
}
