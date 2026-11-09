using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

// ---- Dữ liệu trả cho trang web "Thông tin & chính sách" (JSON snake_case) ----
public sealed record PublicHotel(string Name, string Address, string? Phone, string? Email, string? Website,
    string? CheckInTime, string? CheckOutTime, string? Description);
public sealed record PublicDepositPolicy(string Name, string Type, decimal Value);
public sealed record PublicCancellationRule(int MinimumHours, decimal RefundPercent, decimal FixedFee);
public sealed record PublicCancellationPolicy(string Name, string? Description, IReadOnlyList<PublicCancellationRule> Rules);
public sealed record PublicSurcharge(string Type, string Name, int FromMinute, int? ToMinute, string Calculation, decimal Value);
public sealed record PublicPromotionLine(string RoomTypeCode, string RoomTypeName, string AdjustmentType, decimal Value);
public sealed record PublicPromotion(long Id, string Code, string Name, string? Description, DateOnly From, DateOnly To,
    IReadOnlyList<int> Weekdays, IReadOnlyList<PublicPromotionLine> Lines);
public sealed record PublicWebContent(long Id, string Type, string Title, string Body, string? Tag, string? PeriodText, DateOnly? EventDate);
public sealed record PublicHotelInfo(PublicHotel? Hotel, int HoldMinutes, PublicDepositPolicy? DepositPolicy,
    PublicCancellationPolicy? CancellationPolicy, IReadOnlyList<PublicSurcharge> Surcharges,
    IReadOnlyList<PublicPromotion> Promotions, IReadOnlyList<PublicWebContent> Contents);

// ---- Quản trị nội dung web (App quản lý / Swagger) ----
public sealed record WebContentRequest(
    [param: Required, StringLength(20)] string Type,
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(2000)] string Body,
    [param: StringLength(50)] string? Tag,
    [param: StringLength(150)] string? PeriodText,
    DateOnly? EventDate,
    DateOnly? ShowFrom,
    DateOnly? ShowTo,
    int Order = 0,
    string Status = "HOAT_DONG");
public sealed record WebContentItem(long Id, string Type, string Title, string Body, string? Tag, string? PeriodText,
    DateOnly? EventDate, DateOnly? ShowFrom, DateOnly? ShowTo, int Order, string Status, DateTime UpdatedAt);

/// <summary>
/// Trang "Thông tin &amp; chính sách" của web khách: đọc thông tin khách sạn, chính sách cọc/hủy/phụ thu,
/// chính sách giá đang giảm (ưu đãi) và nội dung tự soạn (noi_dung_web) từ database.
/// </summary>
[ApiController, Authorize, Route("v1")]
public sealed class WebContentController(HotelDbContext db) : ControllerBase
{
    private static readonly string[] DiscountTypes = ["GIAM_%", "GIAM_TIEN"];

    /// <summary>GET /v1/public/hotel-info – không cần đăng nhập.</summary>
    [AllowAnonymous, HttpGet("public/hotel-info")]
    public async Task<ActionResult<PublicHotelInfo>> HotelInfo(CancellationToken ct)
    {
        var today = HotelTime.Today;

        var hotel = await db.Set<KhachSan>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

        // Cùng cách chọn chính sách như khi tạo booking (BookingService): bản mới nhất còn hiệu lực.
        var deposit = await db.Set<ChinhSachCoc>().AsNoTracking()
            .Where(x => x.TrangThai == "HOAT_DONG" && (x.TuNgay == null || x.TuNgay <= today) && (x.DenNgay == null || x.DenNgay >= today))
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var cancellation = await db.Set<ChinhSachHuy>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG")
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        List<PublicCancellationRule> rules = cancellation is null ? [] : await db.Set<ChiTietChinhSachHuy>().AsNoTracking()
            .Where(x => x.ChinhSachHuyId == cancellation.Id).OrderByDescending(x => x.SoGioTruocToiThieu)
            .Select(x => new PublicCancellationRule(x.SoGioTruocToiThieu, x.PhanTramHoan, x.PhiHuyCoDinh)).ToListAsync(ct);

        var surcharges = await db.Set<ChinhSachPhuThu>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG")
            .OrderBy(x => x.Loai).ThenBy(x => x.TuPhut)
            .Select(x => new PublicSurcharge(x.Loai, x.TenChinhSach, x.TuPhut, x.DenPhut, x.KieuTinh, x.GiaTri)).ToListAsync(ct);

        // Ưu đãi = chính sách giá còn hiệu lực có dòng giảm giá (tăng giá mùa cao điểm không hiện ở đây).
        var pricing = await db.ChinhSachGias.AsNoTracking().Include(x => x.CacThuApDung).Include(x => x.ChiTiet)
            .Where(x => x.TrangThai == "HOAT_DONG" && x.DenNgay >= today).OrderBy(x => x.TuNgay).ToListAsync(ct);
        var types = await db.LoaiPhongs.AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG")
            .ToDictionaryAsync(x => x.Id, ct);
        var promotions = pricing
            .Select(p => new PublicPromotion(p.Id, p.MaChinhSach, p.TenChinhSach, p.MoTa, p.TuNgay, p.DenNgay,
                p.CacThuApDung.Select(d => (int)d.Thu).Order().ToList(),
                p.ChiTiet.Where(d => DiscountTypes.Contains(d.KieuDieuChinh) && types.ContainsKey(d.LoaiPhongId))
                    .Select(d => new PublicPromotionLine(types[d.LoaiPhongId].MaLoaiPhong, types[d.LoaiPhongId].TenLoaiPhong, d.KieuDieuChinh, d.GiaTri))
                    .OrderBy(d => d.RoomTypeName).ToList()))
            .Where(p => p.Lines.Count > 0 && p.Weekdays.Count > 0)
            .ToList();

        var contents = await db.Set<NoiDungWeb>().AsNoTracking()
            .Where(x => x.TrangThai == "HOAT_DONG"
                && (x.HienTuNgay == null || x.HienTuNgay <= today)
                && (x.HienDenNgay == null || x.HienDenNgay >= today)
                && (x.Loai != "SU_KIEN" || x.NgaySuKien == null || x.NgaySuKien >= today))
            .OrderBy(x => x.Loai).ThenBy(x => x.ThuTu).ThenBy(x => x.NgaySuKien).ThenBy(x => x.Id)
            .Select(x => new PublicWebContent(x.Id, x.Loai, x.TieuDe, x.NoiDung, x.Nhan, x.ThoiGianHienThi, x.NgaySuKien))
            .ToListAsync(ct);

        return Ok(new PublicHotelInfo(
            hotel is null ? null : new PublicHotel(hotel.TenKhachSan, hotel.DiaChi, hotel.SoDienThoai, hotel.Email, hotel.Website,
                hotel.GioNhanPhong?.ToString("HH:mm"), hotel.GioTraPhong?.ToString("HH:mm"), hotel.MoTa),
            BookingService.HoldMinutes,
            deposit is null ? null : new PublicDepositPolicy(deposit.TenChinhSach, deposit.KieuTinh, deposit.GiaTri),
            cancellation is null ? null : new PublicCancellationPolicy(cancellation.TenChinhSach, cancellation.MoTa, rules),
            surcharges, promotions, contents));
    }

    // ------------------------- Quản trị nội dung web -------------------------

    [HttpGet("web-contents"), RequirePermission("HOTEL_WRITE")]
    public async Task<ActionResult<IReadOnlyList<WebContentItem>>> List(string? type, CancellationToken ct)
        => Ok(await db.Set<NoiDungWeb>().AsNoTracking().Where(x => type == null || x.Loai == type)
            .OrderBy(x => x.Loai).ThenBy(x => x.ThuTu).ThenBy(x => x.Id)
            .Select(x => new WebContentItem(x.Id, x.Loai, x.TieuDe, x.NoiDung, x.Nhan, x.ThoiGianHienThi, x.NgaySuKien,
                x.HienTuNgay, x.HienDenNgay, x.ThuTu, x.TrangThai, x.UpdatedAt))
            .ToListAsync(ct));

    [HttpPost("web-contents"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Create(WebContentRequest r, CancellationToken ct)
    {
        var x = new NoiDungWeb();
        Apply(x, r);
        db.Add(x);
        await db.SaveChangesAsync(ct);
        return Created($"/v1/web-contents/{x.Id}", ToItem(x));
    }

    [HttpPut("web-contents/{id:long}"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Update(long id, WebContentRequest r, CancellationToken ct)
    {
        var x = await Find(id, ct);
        Apply(x, r);
        await db.SaveChangesAsync(ct);
        return Ok(ToItem(x));
    }

    [HttpPatch("web-contents/{id:long}/status"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Status(long id, StatusRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        var x = await Find(id, ct);
        x.TrangThai = r.Status;
        x.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("web-contents/{id:long}"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        db.Remove(await Find(id, ct));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<NoiDungWeb> Find(long id, CancellationToken ct)
        => await db.Set<NoiDungWeb>().SingleOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new ResourceNotFoundException("Không tìm thấy nội dung web.");

    private static void Apply(NoiDungWeb x, WebContentRequest r)
    {
        Input.OneOf(r.Type, NoiDungWeb.Types);
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        if (r.ShowFrom.HasValue && r.ShowTo.HasValue && r.ShowTo < r.ShowFrom)
            throw new InvalidArgumentException("Ngày ẩn phải sau ngày bắt đầu hiển thị.");
        x.Loai = r.Type;
        x.TieuDe = Input.Text(r.Title, 200, "title");
        x.NoiDung = Input.Text(r.Body, 2000, "body");
        x.Nhan = string.IsNullOrWhiteSpace(r.Tag) ? null : r.Tag.Trim();
        x.ThoiGianHienThi = string.IsNullOrWhiteSpace(r.PeriodText) ? null : r.PeriodText.Trim();
        x.NgaySuKien = r.EventDate;
        x.HienTuNgay = r.ShowFrom;
        x.HienDenNgay = r.ShowTo;
        x.ThuTu = r.Order;
        x.TrangThai = r.Status;
        x.UpdatedAt = DateTime.UtcNow;
    }

    private static WebContentItem ToItem(NoiDungWeb x) => new(x.Id, x.Loai, x.TieuDe, x.NoiDung, x.Nhan, x.ThoiGianHienThi,
        x.NgaySuKien, x.HienTuNgay, x.HienDenNgay, x.ThuTu, x.TrangThai, x.UpdatedAt);
}
