using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HotelAdmin.Services;

/// <summary>
/// Gọi API trả JSON "thô" (JsonElement) cho các module quản lý.
/// Các API nghiệp vụ trả entity với tên trường tiếng Việt (snake_case), nên các trang đọc trường theo tên thay vì tạo model riêng.
/// </summary>
public sealed partial class ApiClient
{
    /// <summary>GET trả JSON.</summary>
    public Task<JsonElement> GetJsonAsync(string url, CancellationToken ct = default) => SendRawAsync(HttpMethod.Get, url, null, ct);

    public Task<JsonElement> PostJsonAsync(string url, object? body = null, CancellationToken ct = default) => SendRawAsync(HttpMethod.Post, url, body ?? new { }, ct);

    public Task<JsonElement> PutJsonAsync(string url, object body, CancellationToken ct = default) => SendRawAsync(HttpMethod.Put, url, body, ct);

    public Task<JsonElement> PatchJsonAsync(string url, object body, CancellationToken ct = default) => SendRawAsync(HttpMethod.Patch, url, body, ct);

    /// <summary>Đọc nội dung dạng văn bản (vd. hóa đơn HTML để in).</summary>
    public async Task<string> GetTextAsync(string url, CancellationToken ct = default)
    {
        using var response = await SendSafeAsync(() => http.GetAsync(new Uri(baseUri, url), ct));
        if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(ct);
        await ReadAsync<JsonElement>(response, ct); // ném ApiException với thông báo lỗi chuẩn
        return "";
    }

    /// <summary>Gửi request với mọi phương thức; 204/không có nội dung trả về JsonElement rỗng (ValueKind = Undefined).</summary>
    public async Task<JsonElement> SendRawAsync(HttpMethod method, string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendSafeAsync(async () =>
        {
            using var request = new HttpRequestMessage(method, new Uri(baseUri, url));
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), null, Json);
            return await http.SendAsync(request, ct);
        });
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent) return default;
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text)) return default;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new ApiException(response.StatusCode, "Máy chủ trả dữ liệu không đúng định dạng JSON.");
            }
        }
        return await ReadAsync<JsonElement>(response, ct); // luôn ném ApiException
    }

    /// <summary>Ghép query string: bỏ qua giá trị null/rỗng, tự mã hóa.</summary>
    public static string Url(string path, params (string Key, object? Value)[] query)
    {
        var sb = new StringBuilder(path);
        var first = !path.Contains('?');
        foreach (var (key, value) in query)
        {
            var text = value switch
            {
                null => null,
                DateOnly d => d.ToString("yyyy-MM-dd"),
                DateTime t => t.ToString("yyyy-MM-dd"),
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            if (string.IsNullOrWhiteSpace(text)) continue;
            sb.Append(first ? '?' : '&').Append(key).Append('=').Append(Uri.EscapeDataString(text.Trim()));
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>Mã tham chiếu duy nhất cho giao dịch thu tiền tại quầy (chống thu trùng khi bấm lại).</summary>
    public static string NewReference(string prefix) => $"{prefix}-{DateTime.Now:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
