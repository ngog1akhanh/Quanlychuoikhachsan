using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Rooms;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Form multipart: một hoặc nhiều file ảnh, tên trường "files".</summary>
public sealed class RoomTypeImageUploadForm
{
    public List<IFormFile> Files { get; set; } = [];
}

public sealed record RoomTypeImageItem(long Id, long RoomTypeId, string Url, string? AltText, bool Cover, int Order);

/// <summary>
/// Ảnh loại phòng tải lên từ máy (App quản lý). File được lưu vào thư mục ảnh của web khách
/// (QuanLyKhachSan.Web/wwwroot/images/rooms/photos) với tên {mã loại}-{số}.jpg và ghi vào hinh_anh_loai_phong,
/// nên web hiện ảnh ngay mà không cần chép tay hay khởi động lại.
/// </summary>
[ApiController, Authorize, Route("v1")]
public sealed class RoomTypeImagesController(HotelDbContext db, RoomPhotoStorage storage) : ControllerBase
{
    private const int MaxFiles = 10;

    [HttpGet("room-types/{id:long}/images"), RequirePermission("ROOM_TYPE_READ")]
    public async Task<ActionResult<IReadOnlyList<RoomTypeImageItem>>> List(long id, CancellationToken ct)
        => Ok(await db.Set<HinhAnhLoaiPhong>().AsNoTracking().Where(x => x.LoaiPhongId == id)
            .OrderBy(x => x.ThuTu).ThenBy(x => x.Id)
            .Select(x => new RoomTypeImageItem(x.Id, x.LoaiPhongId, x.Url, x.AltText, x.LaAnhDaiDien, x.ThuTu))
            .ToListAsync(ct));

    /// <summary>POST /v1/room-types/{id}/images:upload (multipart/form-data, trường "files", tối đa 10 ảnh, mỗi ảnh ≤ 5 MB).</summary>
    [HttpPost("room-types/{id:long}/images:upload"), RequirePermission("ROOM_TYPE_WRITE")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<RoomTypeImageItem>>> Upload(long id, [FromForm] RoomTypeImageUploadForm form, CancellationToken ct)
    {
        var type = await db.LoaiPhongs.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ResourceNotFoundException("Không tìm thấy loại phòng.");
        var files = form.Files.Where(f => f.Length > 0).ToList();
        if (files.Count == 0) throw new InvalidArgumentException("Chưa chọn ảnh nào.");
        if (files.Count > MaxFiles) throw new InvalidArgumentException($"Mỗi lần tải tối đa {MaxFiles} ảnh.");
        if (files.Any(f => f.Length > storage.MaxBytes)) throw new InvalidArgumentException("Có ảnh vượt quá dung lượng cho phép (5 MB).");

        // Đọc và kiểm tra toàn bộ file trước khi ghi, để một file lỗi không làm lưu dở dang.
        var contents = new List<byte[]>();
        foreach (var file in files)
        {
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            if (RoomPhotoStorage.DetectExtension(bytes) is null)
                throw new InvalidArgumentException($"\"{file.FileName}\" không phải ảnh JPG, PNG hoặc WEBP.");
            contents.Add(bytes);
        }

        var hasCover = await db.Set<HinhAnhLoaiPhong>().AnyAsync(x => x.LoaiPhongId == id && x.LaAnhDaiDien, ct);
        var saved = new List<string>();
        var created = new List<HinhAnhLoaiPhong>();
        try
        {
            foreach (var bytes in contents)
            {
                var (url, order) = await storage.SaveAsync(type.MaLoaiPhong, bytes, ct);
                saved.Add(url);
                var image = new HinhAnhLoaiPhong
                {
                    LoaiPhongId = id,
                    Url = url,
                    AltText = type.TenLoaiPhong,
                    LaAnhDaiDien = !hasCover && created.Count == 0,
                    ThuTu = order
                };
                created.Add(image);
                db.Add(image);
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Lỗi giữa chừng: xóa các file đã ghi để thư mục ảnh và database không lệch nhau.
            foreach (var url in saved) storage.TryDelete(url);
            throw;
        }

        return Ok(created.Select(x => new RoomTypeImageItem(x.Id, x.LoaiPhongId, x.Url, x.AltText, x.LaAnhDaiDien, x.ThuTu)).ToList());
    }
}
