using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QuanLyKhachSan.Web.State;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Cấu hình lấy từ appsettings.json -> "Api".</summary>
public sealed class ApiOptions
{
    /// <summary>Địa chỉ backend, ví dụ http://localhost:5005</summary>
    public string BaseUrl { get; set; } = "http://localhost:5005";

    /// <summary>true = dùng dữ liệu mẫu (không cần backend). Đổi thành false khi đã có API.</summary>
    public bool UseMockData { get; set; } = true;
}

/// <summary>Lỗi khi gọi API – Message đã là tiếng Việt/đọc được, có thể hiển thị cho người dùng.</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(string message, int statusCode = 0) : base(message) => StatusCode = statusCode;
}

/// <summary>Envelope { data, request_id } mà một số endpoint trả về.</summary>
public sealed record ApiEnvelope<T>(T Data, string? RequestId);

/// <summary>
/// Bọc HttpClient: tự gắn Bearer token, JSON snake_case (khớp backend), đọc lỗi dạng Google-style.
/// </summary>
public sealed class ApiClient
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly AuthState _auth;
    private readonly ApiOptions _options;

    public ApiClient(HttpClient http, AuthState auth, IOptions<ApiOptions> options)
    {
        _http = http;
        _auth = auth;
        _options = options.Value;
    }

    /// <summary>Đang chạy bằng dữ liệu mẫu?</summary>
    public bool UseMock => _options.UseMockData;

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Post, path, body ?? new { }, ct);

    public Task<T> PatchAsync<T>(string path, object body, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Patch, path, body, ct);

    public Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Put, path, body, ct);

    /// <summary>Gọi API trả về { "data": ... } và lấy luôn phần data.</summary>
    public async Task<T> PostEnvelopeAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        var env = await SendAsync<ApiEnvelope<T>>(HttpMethod.Post, path, body ?? new { }, ct);
        return env.Data;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        await _auth.EnsureInitializedAsync();

        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (_auth.IsAuthenticated)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Không kết nối được máy chủ. Vui lòng thử lại sau.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("Máy chủ phản hồi quá chậm. Vui lòng thử lại.");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && _auth.IsAuthenticated)
            {
                await _auth.SignOutAsync();
                throw new ApiException("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.", 401);
            }

            if (!response.IsSuccessStatusCode)
                throw new ApiException(ExtractError(text, (int)response.StatusCode), (int)response.StatusCode);

            if (string.IsNullOrWhiteSpace(text))
                return default!;

            try
            {
                return JsonSerializer.Deserialize<T>(text, JsonOptions)!;
            }
            catch (JsonException)
            {
                throw new ApiException("Dữ liệu máy chủ trả về không đúng định dạng.");
            }
        }
    }

    private static string ExtractError(string text, int status)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            // Google-style: { "error": { "message": "..." } }
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object &&
                err.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString()!;
            // Payment envelope: { "success": false, "message": "..." }
            if (root.TryGetProperty("message", out var m2) && m2.ValueKind == JsonValueKind.String)
                return m2.GetString()!;
        }
        catch (JsonException) { }

        return status switch
        {
            400 => "Dữ liệu gửi lên không hợp lệ.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy dữ liệu hoặc chức năng này chưa được backend hỗ trợ.",
            409 => "Dữ liệu bị trùng hoặc xung đột (ví dụ phòng vừa có người đặt).",
            _ => $"Lỗi máy chủ ({status})."
        };
    }
}
