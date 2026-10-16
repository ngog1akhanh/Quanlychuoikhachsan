using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Phân trang cursor cho đặt phòng, hóa đơn và giao dịch; pageToken là cursor Base64.</summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class PagedResourcesController(HotelDbContext db) : ApiControllerBase
{
    [HttpGet("bookings")]
    public async Task<ActionResult<PageResponse<BookingListItem>>> Bookings([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var rows = await db.DatPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1).Select(x => new BookingListItem(x.Id, $"bookings/{x.Id}", x.MaDatPhong, x.TrangThai, x.DuKienNhan, x.DuKienTra, x.TongTienDuKien)).ToListAsync(ct);
        return OkResponse(new PageResponse<BookingListItem>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size].Id) : null));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<PageResponse<InvoiceResponse>>> Invoices([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var rows = await db.HoaDonPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1).Select(x => new InvoiceResponse(x.Id, $"invoices/{x.Id}", x.DatPhongId, x.TienPhong, x.TienPhuThu, x.TienBoiThuong, x.TienThue, x.TongTien, 0, 0, x.TongTien, x.TrangThai)).ToListAsync(ct);
        return OkResponse(new PageResponse<InvoiceResponse>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size].Id) : null));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<PageResponse<TransactionListItem>>> Transactions([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var rows = await db.ThanhToanHoaDonPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1).Select(x => new TransactionListItem(x.Id, $"transactions/{x.Id}", x.HoaDonPhongId, x.SoTien, x.PhuongThucId, x.ThoiGianThanhToan, x.TrangThai)).ToListAsync(ct);
        return OkResponse(new PageResponse<TransactionListItem>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size].Id) : null));
    }

    private static int Normalize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 100);
    private static long Decode(string? token) { if (string.IsNullOrWhiteSpace(token)) return 0; try { var text = Encoding.UTF8.GetString(Convert.FromBase64String(token)); return long.TryParse(text, out var id) && id >= 0 ? id : throw new FormatException(); } catch (FormatException) { throw new Application.Common.InvalidArgumentException("page_token is invalid."); } }
    private static string Encode(long id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString()));
}