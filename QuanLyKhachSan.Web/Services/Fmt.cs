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

/// <summary>
/// Ảnh phòng. Đặt ảnh thật vào wwwroot/images/rooms/photos/ theo tên: {mã-loại-phòng}-{số}.jpg
/// (vd: std-1.jpg, std-2.jpg, dlx-1.jpg, exe-3.webp ...; hỗ trợ jpg/jpeg/png/webp).
/// Có thể thêm "default-1.jpg"... làm ảnh dùng chung cho loại phòng chưa có ảnh riêng.
/// Loại phòng nào chưa có ảnh thì tự dùng ảnh SVG minh họa cũ.
/// </summary>
public static class RoomImages
{
    private const int TypeCount = 6;
    private static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".webp" };
    private static Dictionary<string, List<string>> _photos = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Quét thư mục ảnh một lần khi khởi động (gọi từ Program.cs).</summary>
    public static void Init(string webRootPath)
    {
        var dir = Path.Combine(webRootPath, "images", "rooms", "photos");
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (!Exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(f);
                var dash = name.LastIndexOf('-');
                var code = dash > 0 ? name[..dash] : name;
                if (!map.TryGetValue(code, out var list)) map[code] = list = new List<string>();
                list.Add("images/rooms/photos/" + Path.GetFileName(f));
            }
        }
        _photos = map;
    }

    private static int Idx(string roomTypeCode)
    {
        var h = 0;
        foreach (var c in roomTypeCode) h = (h * 31 + c) & 0x7fffffff;
        return h % TypeCount;
    }

    private static List<string>? Photos(string code)
        => _photos.TryGetValue(code, out var l) && l.Count > 0 ? l
         : _photos.TryGetValue("default", out var d) && d.Count > 0 ? d : null;

    public static string Cover(PublicRoom r) => Gallery(r)[0];

    public static IReadOnlyList<string> Gallery(PublicRoom r)
    {
        var photos = Photos(r.RoomTypeCode);
        if (photos is not null)
        {
            // Xoay vòng theo phòng để các phòng cùng loại không giống hệt nhau
            var shift = (int)(r.Id % photos.Count);
            return photos.Skip(shift).Concat(photos.Take(shift)).ToList();
        }
        var i = Idx(r.RoomTypeCode);
        return new[]
        {
            $"images/rooms/room-{i}-0.svg",
            $"images/rooms/room-{i}-1.svg",
            $"images/rooms/room-{i}-2.svg"
        };
    }
}
