using Quanlychuoikhachsan.Infrastructure.Authentication;
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
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<PageResponse<BookingListItem>>> Bookings([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var rows = await db.DatPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1).Select(x => new BookingListItem(x.Id, $"bookings/{x.Id}", x.MaDatPhong, x.TrangThai, x.DuKienNhan, x.DuKienTra, x.TongTienDuKien)).ToListAsync(ct);
        return OkResponse(new PageResponse<BookingListItem>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size - 1].Id) : null));
    }

    [HttpGet("invoices")]
    [RequirePermission("BILLING_READ")]
    public async Task<ActionResult<PageResponse<InvoiceResponse>>> Invoices([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var totals = await db.HoaDonPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1)
            .Select(x => new { Invoice = x,
                Deposit = db.ApDungCocHoaDons.Where(a => a.HoaDonPhongId == x.Id).Sum(a => (decimal?)a.SoTienApDung) ?? 0,
                Paid = db.ThanhToanHoaDonPhongs.Where(p => p.HoaDonPhongId == x.Id && p.TrangThai == "THANH_CONG").Sum(p => (decimal?)p.SoTien) ?? 0 }).ToListAsync(ct);
        var rows = totals.Select(t => new InvoiceResponse(t.Invoice.Id, $"invoices/{t.Invoice.Id}", t.Invoice.DatPhongId,
            t.Invoice.TienPhong, t.Invoice.TienPhuThu, t.Invoice.TienBoiThuong, t.Invoice.TienThue, t.Invoice.TongTien,
            t.Deposit, t.Paid, Math.Max(0, t.Invoice.TongTien - t.Deposit - t.Paid), t.Invoice.TrangThai)).ToList();
        return OkResponse(new PageResponse<InvoiceResponse>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size - 1].Id) : null));
    }

    [HttpGet("transactions")]
    [RequirePermission("BILLING_READ")]
    public async Task<ActionResult<PageResponse<TransactionListItem>>> Transactions([FromQuery] PageQuery query, CancellationToken ct)
    {
        var offset = Decode(query.PageToken); var size = Normalize(query.PageSize);
        var rows = await db.ThanhToanHoaDonPhongs.AsNoTracking().OrderBy(x => x.Id).Where(x => x.Id > offset).Take(size + 1).Select(x => new TransactionListItem(x.Id, $"transactions/{x.Id}", x.HoaDonPhongId, x.SoTien, x.PhuongThucId, x.ThoiGianThanhToan, x.TrangThai)).ToListAsync(ct);
        return OkResponse(new PageResponse<TransactionListItem>(rows.Take(size).ToList(), rows.Count > size ? Encode(rows[size - 1].Id) : null));
    }

    private static int Normalize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 100);
    private static long Decode(string? token) { if (string.IsNullOrWhiteSpace(token)) return 0; try { var text = Encoding.UTF8.GetString(Convert.FromBase64String(token)); return long.TryParse(text, out var id) && id >= 0 ? id : throw new FormatException(); } catch (FormatException) { throw new Application.Common.InvalidArgumentException("page_token is invalid."); } }
    private static string Encode(long id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString()));
}
