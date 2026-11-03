using System.Text.RegularExpressions;
namespace QuanLyKhachSan.Web.Models.Room;
public static class BedConfiguration
{
    // Infer only from explicit bed descriptions, never guest capacity.
    public static string? FromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var matches = Regex.Matches(description.ToLowerInvariant(), @"\b(\d+|một|hai|ba|bốn)\s+giường\s+(đơn|đôi|queen|king)\b");
        if (matches.Count == 0) return null;
        return string.Join(" + ", matches.Select(m => new {
            Count = m.Groups[1].Value switch { "một" => 1, "hai" => 2, "ba" => 3, "bốn" => 4, var n => int.Parse(n) },
            Kind = m.Groups[2].Value
        }).GroupBy(x => x.Kind).OrderBy(x => x.Key).Select(g => $"{g.Sum(x => x.Count)} giường {g.Key}"));
    }
}
