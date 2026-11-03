using System.Globalization;
using QuanLyKhachSan.Web.Models.Room;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Hàm định dạng dùng chung trong giao diện.</summary>
public static class Fmt
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static string Money(decimal v) => v.ToString("N0", Vi) + " ₫";
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", Vi);
    public static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", Vi);
    public static string DateTimeText(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vi);
    public static string Weekday(DateOnly d) => d.ToString("ddd, dd/MM", Vi);

    /// <summary>API trả giờ UTC (không kèm 'Z'); chuẩn hóa về UTC để tính đếm ngược.</summary>
    public static System.DateTime AsUtc(System.DateTime d)
        => d.Kind == DateTimeKind.Unspecified ? System.DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
}

/// <summary>Ảnh minh họa phòng (SVG mẫu trong wwwroot/images/rooms). Thay bằng ảnh thật khi có.</summary>
public static class RoomImages
{
    private const int TypeCount = 6;

    private static int Idx(string roomTypeCode)
    {
        var h = 0;
        foreach (var c in roomTypeCode) h = (h * 31 + c) & 0x7fffffff;
        return h % TypeCount;
    }

    public static string Cover(PublicRoom r) => $"images/rooms/room-{Idx(r.RoomTypeCode)}-0.svg";

    public static IReadOnlyList<string> Gallery(PublicRoom r)
    {
        var i = Idx(r.RoomTypeCode);
        return new[]
        {
            $"images/rooms/room-{i}-0.svg",
            $"images/rooms/room-{i}-1.svg",
            $"images/rooms/room-{i}-2.svg"
        };
    }
}
