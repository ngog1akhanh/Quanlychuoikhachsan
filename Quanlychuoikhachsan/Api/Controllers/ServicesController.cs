using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Services;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

[ApiController, Authorize, Route("v1")]
public sealed class ServicesController(HotelDbContext db, CurrentActor actor, ServiceOperations service) : ControllerBase
{
    [AllowAnonymous, HttpGet("services")]
    public async Task<IActionResult> List([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<DichVu>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG" && (q.Search == null || x.TenDichVu.Contains(q.Search))).OrderBy(x => x.Id).PageAsync(q, ct));
    [HttpGet("services/manage"), RequirePermission("SERVICE_READ")]
    public async Task<IActionResult> Manage([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<DichVu>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderBy(x => x.Id).PageAsync(q, ct));
    [HttpPost("services"), RequirePermission("SERVICE_CATALOG_WRITE")]
    public async Task<IActionResult> Create(ServiceRequest r, CancellationToken ct)
    {
        var x = new DichVu(); Apply(x, r); db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/services/{x.Id}", x);
    }
    [HttpPut("services/{id:long}"), RequirePermission("SERVICE_CATALOG_WRITE")]
    public async Task<IActionResult> Update(long id, ServiceRequest r, CancellationToken ct)
    {
        var x = await db.Set<DichVu>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Service was not found."); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x);
    }
    private static void Apply(DichVu x, ServiceRequest r)
    {
        Input.Money(r.Price, "price"); Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        x.MaDichVu = Input.Text(r.Code, 30, "code"); x.TenDichVu = Input.Text(r.Name, 120, "name"); x.NhomDichVu = r.Group; x.DonViTinh = Input.Text(r.Unit, 30, "unit"); x.DonGia = r.Price; x.MoTa = r.Description; x.TrangThai = r.Status;
    }
    [HttpPost("service-requests")]
    public async Task<IActionResult> CreateRequest(UseServiceRequest r, CancellationToken ct) { var row = await service.RequestAsync(r, ct); return Created($"/v1/service-requests/{row.Id}", row); }
    [HttpGet("service-requests")]
    public async Task<IActionResult> Requests([FromQuery] ListQuery q, long? bookingId, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("SERVICE_READ", ct); var account = actor.AccountId;
        return Ok(await db.Set<SuDungDichVu>().AsNoTracking().Where(x => (staff || db.DatPhongs.Any(b => b.Id == x.DatPhongId && b.KhachHang.TaiKhoanId == account))
            && (!bookingId.HasValue || x.DatPhongId == bookingId) && (q.Status == null || x.TrangThai == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpPatch("service-requests/{id:long}/status"), RequirePermission("SERVICE_WRITE")]
    public async Task<IActionResult> Status(long id, StatusRequest r, CancellationToken ct) => Ok(await service.TransitionAsync(id, r.Status, ct));
    [HttpGet("service-invoices")]
    public async Task<IActionResult> Invoices([FromQuery] ListQuery q, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BILLING_READ", ct); var account = actor.AccountId;
        return Ok(await db.Set<HoaDonDichVu>().AsNoTracking().Where(x => staff || db.DatPhongs.Any(b => b.Id == x.DatPhongId && b.KhachHang.TaiKhoanId == account)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpGet("service-invoices/{id:long}")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var invoice = await db.Set<HoaDonDichVu>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Invoice was not found.");
        await actor.BookingAsync(invoice.DatPhongId, "BILLING_READ", ct);
        var lines = await db.Set<ChiTietHoaDonDichVu>().Where(x => x.HoaDonDichVuId == id).ToListAsync(ct);
        var payments = await db.Set<ThanhToanDichVu>().Where(x => x.HoaDonDichVuId == id).ToListAsync(ct);
        return Ok(new { Invoice = invoice, Lines = lines, Payments = payments, Due = invoice.TongTien - payments.Where(x => x.TrangThai == "THANH_CONG").Sum(x => x.SoTien) });
    }
    [HttpPost("service-invoices/{id:long}:pay"), RequirePermission("BILLING_PAYMENT_WRITE")]
    public async Task<IActionResult> Pay(long id, ServicePaymentRequest r, CancellationToken ct) => Ok(await service.PayAsync(id, r, ct));
    [HttpPost("service-invoices/{id:long}:void"), RequirePermission("BILLING_WRITE")]
    public async Task<IActionResult> Void(long id, ReasonRequest r, CancellationToken ct) => Ok(await service.VoidAsync(id, r.Reason, ct));
}
