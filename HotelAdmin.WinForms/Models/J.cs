using System.Globalization;
using System.Text.Json;

namespace HotelAdmin.Models;

/// <summary>
/// Đọc an toàn JsonElement trả về từ API: thiếu trường hoặc sai kiểu thì trả giá trị mặc định thay vì ném lỗi.
/// Đường dẫn dùng dấu chấm cho trường lồng nhau, ví dụ "invoice.total".
/// </summary>
public static class J
{
    public static JsonElement? Get(JsonElement e, string path)
    {
        var current = e;
        foreach (var part in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var next)) return null;
            current = next;
        }
        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : current;
    }

    public static bool Has(JsonElement e, string path) => Get(e, path) is not null;

    public static string Str(JsonElement e, string path, string fallback = "")
    {
        if (Get(e, path) is not { } v) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? fallback,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => v.GetRawText(),
            _ => v.GetRawText()
        };
    }

    public static string? StrOrNull(JsonElement e, string path)
    {
        var s = Str(e, path);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public static decimal Dec(JsonElement e, string path, decimal fallback = 0)
    {
        if (Get(e, path) is not { } v) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
        return fallback;
    }

    public static long Long(JsonElement e, string path, long fallback = 0)
    {
        if (Get(e, path) is not { } v) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)) return l;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out l)) return l;
        return fallback;
    }

    public static long? LongOrNull(JsonElement e, string path) => Get(e, path) is null ? null : Long(e, path);

    public static int Int(JsonElement e, string path, int fallback = 0) => (int)Long(e, path, fallback);

    public static bool Bool(JsonElement e, string path)
        => Get(e, path) is { } v && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && v.GetString() == "true"));

    /// <summary>
    /// Thời điểm do API trả về. Chuỗi có Z/offset được đổi sang giờ máy; chuỗi không có múi giờ được coi là UTC
    /// (backend lưu UTC) trừ khi <paramref name="assumeLocal"/> = true.
    /// </summary>
    public static DateTime? Time(JsonElement e, string path, bool assumeLocal = false)
    {
        var s = Str(e, path);
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) return null;
        return t.Kind switch
        {
            DateTimeKind.Utc => t.ToLocalTime(),
            DateTimeKind.Local => t,
            _ => assumeLocal || s.Length <= 10 ? t : DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime()
        };
    }

    /// <summary>Ngày thuần (DateOnly dạng yyyy-MM-dd).</summary>
    public static DateTime? Day(JsonElement e, string path)
    {
        var s = Str(e, path);
        return DateTime.TryParseExact(s.Length >= 10 ? s[..10] : s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>Danh sách dòng: mảng gốc, hoặc trường items / data / customers / data.items.</summary>
    public static List<JsonElement> Items(JsonElement e, string? path = null)
    {
        var target = path is null ? e : Get(e, path) ?? default;
        if (target.ValueKind == JsonValueKind.Array) return target.EnumerateArray().ToList();
        if (target.ValueKind != JsonValueKind.Object) return [];
        foreach (var key in new[] { "items", "data", "customers" })
            if (target.TryGetProperty(key, out var inner))
            {
                if (inner.ValueKind == JsonValueKind.Array) return inner.EnumerateArray().ToList();
                if (inner.ValueKind == JsonValueKind.Object) return Items(inner);
            }
        return [];
    }

    /// <summary>Tổng số dòng của danh sách phân trang (total / total_size); không có thì -1.</summary>
    public static int Total(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return -1;
        if (Get(e, "total") is not null) return Int(e, "total");
        if (Get(e, "total_size") is not null) return Int(e, "total_size");
        return -1;
    }

    /// <summary>Bỏ lớp {data, request_id} nếu có.</summary>
    public static JsonElement Unwrap(JsonElement e)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("data", out var d) && e.TryGetProperty("request_id", out _) ? d : e;

    // ---------------- Định dạng hiển thị ----------------
    public static string Money(decimal v) => v.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN"));
    public static string MoneyVnd(decimal v) => Money(v) + " đ";
    public static string Qty(decimal v) => v == decimal.Truncate(v) ? v.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) : v.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));
    public static string DateText(DateTime? v) => v?.ToString("dd/MM/yyyy") ?? "";
    public static string TimeText(DateTime? v) => v?.ToString("dd/MM/yyyy HH:mm") ?? "";
}
