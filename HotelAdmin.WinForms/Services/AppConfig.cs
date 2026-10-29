using System.Text.Json;

namespace HotelAdmin.Services;

/// <summary>Cấu hình đọc từ appsettings.json nằm cạnh file .exe.</summary>
public sealed class AppConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5005/";
    public string HotelName { get; set; } = "Lotus Hotel";
    public int RequestTimeoutSeconds { get; set; } = 20;

    public static AppConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(path))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });
                if (config is not null)
                {
                    if (!config.ApiBaseUrl.EndsWith('/')) config.ApiBaseUrl += "/";
                    return config;
                }
            }
        }
        catch (JsonException)
        {
            // File cấu hình lỗi -> dùng giá trị mặc định.
        }
        return new AppConfig();
    }
}
