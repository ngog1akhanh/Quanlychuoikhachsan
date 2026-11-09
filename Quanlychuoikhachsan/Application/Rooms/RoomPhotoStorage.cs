using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Common;

namespace Quanlychuoikhachsan.Application.Rooms;

/// <summary>Cấu hình appsettings.json -> "RoomPhotos".</summary>
public sealed class RoomPhotoOptions
{
    public const string SectionName = "RoomPhotos";

    /// <summary>
    /// Thư mục lưu ảnh loại phòng = thư mục ảnh của web khách (đường dẫn tương đối tính từ thư mục project API).
    /// Web đọc ảnh theo tên file: {mã loại phòng}-{số}.jpg, ví dụ std-1.jpg, vip-2.png.
    /// </summary>
    public string Folder { get; set; } = "../QuanLyKhachSan.Web/wwwroot/images/rooms/photos";

    /// <summary>Đường dẫn ảnh lưu vào hinh_anh_loai_phong.url (tương đối với gốc web khách).</summary>
    public string UrlPrefix { get; set; } = "images/rooms/photos/";

    public int MaxFileMb { get; set; } = 5;
}

/// <summary>Lưu / xóa file ảnh loại phòng trong thư mục photos của web khách.</summary>
public sealed class RoomPhotoStorage(IOptions<RoomPhotoOptions> options, IHostEnvironment env)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly RoomPhotoOptions settings = options.Value;

    public int MaxBytes => settings.MaxFileMb * 1024 * 1024;

    public string FolderPath => Path.GetFullPath(Path.IsPathRooted(settings.Folder)
        ? settings.Folder
        : Path.Combine(env.ContentRootPath, settings.Folder));

    /// <summary>Tiền tố tên file theo mã loại phòng: chữ thường, chỉ giữ a-z 0-9 _ -.</summary>
    public static string Prefix(string roomTypeCode)
        => Regex.Replace(roomTypeCode.Trim().ToLowerInvariant(), "[^a-z0-9_-]", "_");

    /// <summary>Kiểm tra định dạng thật của file (không tin phần đuôi), trả về đuôi file hoặc null.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return ".jpg";
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    /// <summary>Lưu ảnh thành {prefix}-{số tiếp theo}.{đuôi}; trả về (url tương đối, số thứ tự).</summary>
    public async Task<(string Url, int Order)> SaveAsync(string roomTypeCode, byte[] content, CancellationToken ct)
    {
        if (content.Length == 0) throw new InvalidArgumentException("File ảnh rỗng.");
        if (content.Length > MaxBytes) throw new InvalidArgumentException($"Mỗi ảnh tối đa {settings.MaxFileMb} MB.");
        var extension = DetectExtension(content) ?? throw new InvalidArgumentException("Chỉ nhận ảnh JPG, PNG hoặc WEBP.");

        var folder = FolderPath;
        Directory.CreateDirectory(folder);
        var prefix = Prefix(roomTypeCode);
        var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)\\.(jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase);

        await Gate.WaitAsync(ct);
        try
        {
            var next = Directory.EnumerateFiles(folder)
                .Select(f => pattern.Match(Path.GetFileName(f)))
                .Where(m => m.Success && int.TryParse(m.Groups[1].Value, out _))
                .Select(m => int.Parse(m.Groups[1].Value))
                .DefaultIfEmpty(0).Max() + 1;
            var fileName = $"{prefix}-{next}{extension}";
            // CreateNew: không bao giờ ghi đè ảnh đã có.
            await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await stream.WriteAsync(content, ct);
            return (settings.UrlPrefix + fileName, next);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Xóa file ảnh do hệ thống quản lý (url bắt đầu bằng UrlPrefix). Url ngoài (https://…) thì bỏ qua.</summary>
    public void TryDelete(string url)
    {
        if (!url.StartsWith(settings.UrlPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var name = Path.GetFileName(url[settings.UrlPrefix.Length..]);
        if (string.IsNullOrEmpty(name)) return;
        var path = Path.GetFullPath(Path.Combine(FolderPath, name));
        if (!path.StartsWith(FolderPath, StringComparison.OrdinalIgnoreCase)) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* file đang mở: giữ lại, bản ghi DB vẫn xóa */ }
        catch (UnauthorizedAccessException) { }
    }
}
