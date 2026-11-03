using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record RefundRequest(long? DepositId, long? RoomPaymentId, long? ServicePaymentId, long PaymentMethodId, decimal Amount, [param: Required, StringLength(1000)] string Reason);
public sealed record RefundConfirmRequest([param: Required, StringLength(150)] string Reference, long? PaymentMethodId = null);
public sealed record OpenShiftRequest(decimal OpeningCash);
public sealed record ReconcileLine(long PaymentMethodId, decimal ActualTotal, [param: StringLength(500)] string? Note);
public sealed record CloseShiftRequest([param: Required, MaxLength(100)] IReadOnlyList<ReconcileLine> Lines);

[ApiController, Authorize, Route("v1")]
public sealed class BillingManagementController(HotelDbContext db, CurrentActor actor, BookingService bookings, NotificationWriter notifications) : ControllerBase
{
    [HttpGet("payment-methods")]
    public async Task<IActionResult> Methods(CancellationToken ct) => Ok(await db.PhuongThucThanhToans.AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").ToListAsync(ct));
    [HttpGet("invoices/{id:long}")]
    public async Task<IActionResult> Invoice(long id, CancellationToken ct)
    {
        var x = await db.HoaDonPhongs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Invoice was not found."); await actor.BookingAsync(x.DatPhongId, "BILLING_READ", ct);
        return Ok(new { Invoice = await bookings.InvoiceResponseAsync(x, ct), Lines = await db.ChiTietHoaDonPhongs.AsNoTracking().Where(l => l.HoaDonPhongId == id).ToListAsync(ct), Payments = await db.ThanhToanHoaDonPhongs.AsNoTracking().Where(p => p.HoaDonPhongId == id).ToListAsync(ct) });
    }
    [HttpGet("invoices/{id:long}/print")]
    public async Task<IActionResult> Print(long id, CancellationToken ct)
    {
        var x = await db.HoaDonPhongs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Invoice was not found."); await actor.BookingAsync(x.DatPhongId, "BILLING_READ", ct);
        var lines = await db.ChiTietHoaDonPhongs.AsNoTracking().Where(l => l.HoaDonPhongId == id).ToListAsync(ct); var summary = await bookings.InvoiceResponseAsync(x, ct);
        var html = new StringBuilder("<!doctype html><html lang='vi'><meta charset='utf-8'><title>Hóa đơn khách sạn</title><style>body{font:16px sans-serif;max-width:900px;margin:40px auto}table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #ddd;padding:12px;text-align:left}</style><h1>Hóa đơn tiền phòng</h1>");
        html.Append($"<p>{WebUtility.HtmlEncode(x.MaHoaDon)} · {WebUtility.HtmlEncode(x.TrangThai)}</p><table><tr><th>Nội dung</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr>");
        foreach (var l in lines) html.Append($"<tr><td>{WebUtility.HtmlEncode(l.NoiDung)}</td><td>{l.SoLuong:N2}</td><td>{l.DonGia:N2}</td><td>{l.ThanhTien:N2}</td></tr>");
        html.Append($"</table><p>Tổng: {summary.Total:N2} VND</p><p>Cọc áp dụng: {summary.DepositApplied:N2} VND</p><p>Đã thanh toán: {summary.Paid:N2} VND</p><p>Còn phải thu: {summary.Due:N2} VND</p></html>");
        return Content(html.ToString(), "text/html; charset=utf-8");
    }
    [HttpPost("invoices/{id:long}:send"), RequirePermission("BILLING_WRITE")]
    public async Task<IActionResult> Send(long id, CancellationToken ct)
    {
        var x = await db.HoaDonPhongs.SingleOrDefaultAsync(x => x.Id == id && x.TrangThai != "NHAP", ct) ?? throw new ResourceNotFoundException("Issued invoice was not found.");
        var booking = await db.DatPhongs.Include(b => b.KhachHang).SingleAsync(b => b.Id == x.DatPhongId, ct);
        if (!booking.KhachHang.TaiKhoanId.HasValue) throw new InvalidArgumentException("Walk-in customer has no website account.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await notifications.SendAsync("PAYMENT", "Hóa đơn tiền phòng", $"Hóa đơn {x.MaHoaDon}: /v1/invoices/{id}/print", [booking.KhachHang.TaiKhoanId.Value], actor.AccountId, booking, ct); await tx.CommitAsync(ct); return NoContent();
    }
    [HttpPost("invoices/{id:long}:void"), RequirePermission("BILLING_WRITE")]
    public async Task<IActionResult> Void(long id, ReasonRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.HoaDonPhongs.FromSqlInterpolated($"SELECT * FROM hoa_don_phong WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var x = await db.HoaDonPhongs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Invoice was not found.");
        if (x.TrangThai == "DA_HUY") throw new InvalidArgumentException("Invoice is already voided.");
        if (await db.YeuCauThanhToanVietQrs.AnyAsync(p => p.HoaDonPhongId == id && p.TrangThai != "PAID" && p.TrangThai != "REFUNDED", ct)) throw new InvalidArgumentException("Resolve outstanding VietQR orders before voiding.");
        x.TrangThai = "DA_HUY"; x.LyDoHuy = r.Reason; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent();
    }
    [HttpGet("deposits")]
    public async Task<IActionResult> Deposits([FromQuery] ListQuery q, long? bookingId, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BILLING_READ", ct);
        return Ok(await db.PhieuCocs.AsNoTracking().Where(x => (!bookingId.HasValue || x.DatPhongId == bookingId) && (staff || db.DatPhongs.Any(b => b.Id == x.DatPhongId && b.KhachHang.TaiKhoanId == actor.AccountId))).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpGet("refunds"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Refunds([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<PhieuHoanTien>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpPost("refunds"), RequirePermission("REFUND_WRITE")]
    public async Task<IActionResult> CreateRefund(RefundRequest r, CancellationToken ct)
    {
        Input.Money(r.Amount, "amount"); if (r.Amount <= 0 || (r.DepositId.HasValue ? 1 : 0) + (r.RoomPaymentId.HasValue ? 1 : 0) + (r.ServicePaymentId.HasValue ? 1 : 0) != 1) throw new InvalidArgumentException("A positive amount and exactly one source payment are required.");
        if (!await db.PhuongThucThanhToans.AnyAsync(x => x.Id == r.PaymentMethodId && x.TrangThai == "HOAT_DONG", ct)) throw new ResourceNotFoundException("Payment method was not found.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); decimal available;
        if (r.DepositId.HasValue)
        {
            var source = await db.PhieuCocs.SingleOrDefaultAsync(x => x.Id == r.DepositId && x.TrangThai == "DA_THANH_TOAN", ct) ?? throw new ResourceNotFoundException("Deposit was not found.");
            var booking = await db.DatPhongs.SingleAsync(x => x.Id == source.DatPhongId, ct);
            if (booking.TrangThai is not ("DA_HUY" or "DA_TRA")) throw new InvalidArgumentException("Deposit refunds require a cancelled or completed booking.");
            var applied = await db.ApDungCocHoaDons.Where(a => a.PhieuCocId == source.Id && db.HoaDonPhongs.Any(i => i.Id == a.HoaDonPhongId && i.TrangThai != "DA_HUY")).SumAsync(a => (decimal?)a.SoTienApDung, ct) ?? 0;
            var reserved = await db.Set<PhieuHoanTien>().Where(x => x.PhieuCocId == source.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
            available = source.SoTien - applied - reserved;
            if (booking.TrangThai == "DA_HUY")
            {
                var approved = await db.YeuCauHuyDatPhongs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_DUYET").SumAsync(x => (decimal?)x.SoTienHoanDuyet, ct) ?? 0;
                var allReserved = await db.Set<PhieuHoanTien>().Where(x => x.PhieuCocId.HasValue && db.PhieuCocs.Any(d => d.Id == x.PhieuCocId && d.DatPhongId == booking.Id) && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
                available = Math.Min(available, approved - allReserved);
            }
        }
        else if (r.RoomPaymentId.HasValue)
        {
            var source = await db.ThanhToanHoaDonPhongs.SingleOrDefaultAsync(x => x.Id == r.RoomPaymentId && x.TrangThai == "THANH_CONG", ct) ?? throw new ResourceNotFoundException("Payment was not found.");
            if (!await db.HoaDonPhongs.AnyAsync(x => x.Id == source.HoaDonPhongId && x.TrangThai == "DA_HUY", ct)) throw new InvalidArgumentException("Void the source invoice before refunding its payment.");
            available = source.SoTien - (await db.Set<PhieuHoanTien>().Where(x => x.ThanhToanHdpId == source.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0);
        }
        else
        {
            var source = await db.Set<ThanhToanDichVu>().SingleOrDefaultAsync(x => x.Id == r.ServicePaymentId && x.TrangThai == "THANH_CONG", ct) ?? throw new ResourceNotFoundException("Payment was not found.");
            if (!await db.Set<HoaDonDichVu>().AnyAsync(x => x.Id == source.HoaDonDichVuId && x.TrangThai == "DA_HUY", ct)) throw new InvalidArgumentException("Void the source service invoice before refunding.");
            available = source.SoTien - (await db.Set<PhieuHoanTien>().Where(x => x.ThanhToanDvId == source.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0);
        }
        if (r.Amount > available) throw new InvalidArgumentException("Refund exceeds available refundable amount.");
        var row = new PhieuHoanTien { MaPhieuHoan = $"HT{Guid.NewGuid():N}", PhieuCocId = r.DepositId, ThanhToanHdpId = r.RoomPaymentId, ThanhToanDvId = r.ServicePaymentId, PhuongThucId = r.PaymentMethodId, NhanVienId = await actor.EmployeeIdAsync(ct), SoTien = r.Amount, LyDo = r.Reason, TrangThai = "CHO_XU_LY", ThoiGianHoan = DateTime.UtcNow };
        db.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Created($"/v1/refunds/{row.Id}", row);
    }
    [HttpPost("refunds/{id:long}:confirm"), RequirePermission("REFUND_CONFIRM")]
    public async Task<IActionResult> ConfirmRefund(long id, RefundConfirmRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.Set<PhieuHoanTien>().FromSqlInterpolated($"SELECT * FROM phieu_hoan_tien WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var x = await db.Set<PhieuHoanTien>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Refund was not found.");
        Input.Text(r.Reference, 150, "reference");
        if (x.TrangThai == "THANH_CONG" && x.MaGiaoDich == r.Reference && (!r.PaymentMethodId.HasValue || r.PaymentMethodId == x.PhuongThucId)) return Ok(x);
        if (x.TrangThai != "CHO_XU_LY") throw new InvalidArgumentException("Refund cannot be confirmed.");
        // A QR receipt can be refunded by a separately verified bank transfer.
        // This endpoint records a refund already performed by the cashier.
        if (r.PaymentMethodId.HasValue) x.PhuongThucId = r.PaymentMethodId.Value;
        var method = await db.PhuongThucThanhToans.SingleOrDefaultAsync(m => m.Id == x.PhuongThucId && m.TrangThai == "HOAT_DONG", ct) ?? throw new ResourceNotFoundException("Active refund method was not found.");
        Input.OneOf(method.MaPhuongThuc, "TIEN_MAT", "CHUYEN_KHOAN", "THE", "CASH", "BANK_TRANSFER", "CARD");
        if (await db.Set<PhieuHoanTien>().AnyAsync(p => p.Id != id && p.MaGiaoDich == r.Reference && p.PhuongThucId == x.PhuongThucId, ct)) throw new AlreadyExistsException("Refund reference was already used.");
        x.TrangThai = "THANH_CONG"; x.MaGiaoDich = r.Reference; x.ThoiGianHoan = DateTime.UtcNow; x.NhanVienId = await actor.EmployeeIdAsync(ct);
        await db.SaveChangesAsync(ct);
        if (x.PhieuCocId.HasValue)
        {
            var deposit = await db.PhieuCocs.SingleAsync(p => p.Id == x.PhieuCocId, ct);
            var refunded = await db.Set<PhieuHoanTien>().Where(p => p.PhieuCocId == deposit.Id && p.TrangThai == "THANH_CONG").SumAsync(p => p.SoTien, ct);
            if (refunded >= deposit.SoTien)
            {
                var order = await db.Set<DepositPaymentOrder>().SingleOrDefaultAsync(p => p.BookingId == deposit.DatPhongId && p.TransactionId == deposit.MaGiaoDich, ct);
                if (order is not null) order.Status = "REFUNDED";
            }
            var booking = await db.DatPhongs.Include(b => b.KhachHang).SingleAsync(b => b.Id == deposit.DatPhongId, ct);
            if (booking.KhachHang.TaiKhoanId.HasValue)
                await notifications.SendAsync("PAYMENT", "Đã hoàn tiền cọc", $"Phiếu hoàn {x.MaPhieuHoan}: {x.SoTien:N2} VND.", [booking.KhachHang.TaiKhoanId.Value], actor.AccountId, booking, ct);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(x);
    }
    [HttpGet("shifts"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Shifts([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.CaLamViecs.AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpPost("shifts"), RequirePermission("BILLING_PAYMENT_WRITE")]
    public async Task<IActionResult> Open(OpenShiftRequest r, CancellationToken ct)
    {
        Input.Money(r.OpeningCash, "opening_cash"); var employee = await actor.EmployeeIdAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.CaLamViecs.AnyAsync(x => x.NhanVienId == employee && x.TrangThai == "DANG_MO", ct)) throw new AlreadyExistsException("Employee already has an open shift.");
        var x = new CaLamViec { NhanVienId = employee, BatDauLuc = DateTime.UtcNow, TienDauCa = r.OpeningCash, TrangThai = "DANG_MO" }; db.Add(x); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Created($"/v1/shifts/{x.Id}", x);
    }
    [HttpGet("shifts/{id:long}/reconciliation"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Reconciliation(long id, CancellationToken ct) => Ok(new { Totals = await ShiftTotals(id, ct), Reconciliations = await db.Set<DoiSoatCa>().AsNoTracking().Where(x => x.CaLamViecId == id).ToListAsync(ct) });
    private async Task<Dictionary<long, decimal>> ShiftTotals(long id, CancellationToken ct)
    {
        if (!await db.CaLamViecs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Shift was not found.");
        var room = await db.ThanhToanHoaDonPhongs.Where(x => x.CaLamViecId == id && x.TrangThai == "THANH_CONG").Select(x => new { x.PhuongThucId, x.SoTien }).ToListAsync(ct);
        var service = await db.Set<ThanhToanDichVu>().Where(x => x.CaLamViecId == id && x.TrangThai == "THANH_CONG").Select(x => new { x.PhuongThucId, x.SoTien }).ToListAsync(ct);
        var deposits = await db.PhieuCocs.Where(x => x.CaLamViecId == id && x.TrangThai == "DA_THANH_TOAN").Select(x => new { x.PhuongThucId, x.SoTien }).ToListAsync(ct);
        return room.Concat(service).Concat(deposits).GroupBy(x => x.PhuongThucId).ToDictionary(g => g.Key, g => g.Sum(x => x.SoTien));
    }
    [HttpPost("shifts/{id:long}:close"), RequirePermission("BILLING_PAYMENT_WRITE")]
    public async Task<IActionResult> Close(long id, CloseShiftRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.CaLamViecs.FromSqlInterpolated($"SELECT * FROM ca_lam_viec WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var x = await db.CaLamViecs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Shift was not found.");
        if (x.NhanVienId != await actor.EmployeeIdAsync(ct) || x.TrangThai != "DANG_MO") throw new InvalidArgumentException("Only the cashier can close an open shift.");
        var totals = await ShiftTotals(id, ct);
        if (r.Lines.Select(l => l.PaymentMethodId).Distinct().Count() != r.Lines.Count || totals.Keys.Except(r.Lines.Select(l => l.PaymentMethodId)).Any()) throw new InvalidArgumentException("Provide each collected payment method exactly once.");
        foreach (var line in r.Lines)
        {
            Input.Money(line.ActualTotal, "actual_total"); if (!await db.PhuongThucThanhToans.AnyAsync(m => m.Id == line.PaymentMethodId, ct)) throw new InvalidArgumentException("Unknown payment method.");
            var total = totals.GetValueOrDefault(line.PaymentMethodId); db.Add(new DoiSoatCa { CaLamViecId = id, PhuongThucId = line.PaymentMethodId, TongHeThong = total, TongThucTe = line.ActualTotal, ChenhLech = line.ActualTotal - total, GhiChu = line.Note, DoiSoatLuc = DateTime.UtcNow });
        }
        x.TrangThai = "DA_DONG"; x.KetThucLuc = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(x);
    }
}
