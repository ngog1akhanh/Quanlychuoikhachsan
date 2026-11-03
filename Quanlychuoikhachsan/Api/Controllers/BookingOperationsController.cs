using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record CounterBookingRequest(long CustomerId, string Channel, CreateBookingRequest Booking);
public sealed record RescheduleRequest(DateTime CheckIn, DateTime CheckOut, [param: Required, StringLength(1000)] string Reason);
public sealed record MoveRoomRequest(long RoomId, [param: Required, StringLength(1000)] string Reason);
public sealed record StayGuestRequest([param: Required, StringLength(120)] string FullName, DateOnly? BirthDate, [param: StringLength(20)] string? Gender,
    [param: StringLength(30)] string? DocumentType, [param: Required, StringLength(50)] string DocumentNumber, [param: StringLength(80)] string? Nationality, bool Representative);
public sealed record BookingChangeRequest(string Type, [param: Required, StringLength(1500)] string Content, DateTime? NewCheckIn, DateTime? NewCheckOut, long? NewRoomId);
public sealed record ResolveChangeRequest([param: Required, StringLength(1500)] string Result, bool Completed);

[ApiController, Authorize, Route("v1")]
public sealed class BookingOperationsController(HotelDbContext db, CurrentActor actor, BookingService service, NotificationWriter notifications) : ControllerBase
{
    [HttpGet("bookings/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var x = await actor.BookingAsync(id, "BOOKING_READ", ct);
        var details = await db.ChiTietDatPhongs.AsNoTracking().Where(d => d.DatPhongId == id).Select(d => new { d.Id, d.PhongId, d.SoNguoiLon, d.SoTreEm, d.TrangThai, d.NhanPhongLuc, d.TraPhongLuc, d.TienPhongDuKien }).ToListAsync(ct);
        var ids = details.Select(d => d.Id).ToArray(); var nights = await db.GiaDemDatPhongs.AsNoTracking().Where(n => ids.Contains(n.ChiTietDatPhongId)).OrderBy(n => n.NgayLuuTru).ToListAsync(ct);
        var deposits = await db.PhieuCocs.AsNoTracking().Where(p => p.DatPhongId == id).Select(p => new { p.Id, p.SoTien, p.ThoiGianCoc, p.TrangThai, p.PhuongThucId }).ToListAsync(ct);
        var required = await service.RequiredDepositAsync(x, ct);
        var refunds = await db.Set<PhieuHoanTien>().AsNoTracking().Where(r => r.PhieuCocId.HasValue && db.PhieuCocs.Any(p => p.Id == r.PhieuCocId && p.DatPhongId == id)).ToListAsync(ct);
        var netDeposit = deposits.Where(p => p.TrangThai == "DA_THANH_TOAN").Sum(p => p.SoTien) - refunds.Where(r => r.TrangThai == "THANH_CONG").Sum(r => r.SoTien);
        return Ok(new { x.Id, x.MaDatPhong, x.KhachHangId, x.TrangThai, x.DuKienNhan, x.DuKienTra, x.SoNguoiLon, x.SoTreEm, x.YeuCauDacBiet, x.TongTienDuKien, Details = details, Nights = nights, Deposits = deposits,
            RequiredDeposit = required, DepositShortfall = Math.Max(0, required - netDeposit), Refunds = refunds, ExpectedRefund = await service.ExpectedRefundAsync(x, ct) });
    }
    [HttpGet("bookings/search"), RequirePermission("BOOKING_READ")]
    public async Task<IActionResult> Search([FromQuery] ListQuery q, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue) Input.Range(from.Value, to.Value);
        var start = from?.ToDateTime(TimeOnly.MinValue); var end = to?.ToDateTime(TimeOnly.MaxValue);
        return Ok(await db.DatPhongs.AsNoTracking().Where(x => (q.Status == null || x.TrangThai == q.Status) && (q.Search == null || x.MaDatPhong.Contains(q.Search) || x.KhachHang.HoTen.Contains(q.Search) || (x.KhachHang.SoDienThoai != null && x.KhachHang.SoDienThoai.Contains(q.Search)))
            && (!start.HasValue || x.DuKienTra >= start) && (!end.HasValue || x.DuKienNhan <= end)).OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.MaDatPhong, x.KhachHang.HoTen, x.KhachHang.SoDienThoai, x.DuKienNhan, x.DuKienTra, x.TrangThai, x.KenhDat, x.TongTienDuKien }).PageAsync(q, ct));
    }
    [HttpGet("bookings/arrivals-departures"), RequirePermission("BOOKING_READ")]
    public async Task<IActionResult> Arrivals(DateOnly date, CancellationToken ct)
    {
        var from = date.ToDateTime(TimeOnly.MinValue); var to = from.AddDays(1);
        return Ok(await db.DatPhongs.AsNoTracking().Where(x => x.TrangThai != "DA_HUY" && ((x.DuKienNhan >= from && x.DuKienNhan < to) || (x.DuKienTra >= from && x.DuKienTra < to)))
            .Select(x => new { x.Id, x.MaDatPhong, x.KhachHang.HoTen, x.DuKienNhan, x.DuKienTra, x.TrangThai }).ToListAsync(ct));
    }
    [HttpPost("bookings/counter"), RequirePermission("BOOKING_WRITE")]
    public async Task<IActionResult> Counter(CounterBookingRequest r, CancellationToken ct) => Ok(await service.CreateCounterBookingAsync(r.CustomerId, r.Channel, r.Booking, ct));
    [HttpPost("bookings/{id:long}:reschedule"), RequirePermission("BOOKING_WRITE")]
    public async Task<IActionResult> Reschedule(long id, RescheduleRequest r, CancellationToken ct) => Ok(await service.RescheduleAsync(id, r.CheckIn, r.CheckOut, r.Reason, ct));
    [HttpPost("bookings/{id:long}/rooms/{detailId:long}:move"), RequirePermission("BOOKING_WRITE")]
    public async Task<IActionResult> Move(long id, long detailId, MoveRoomRequest r, CancellationToken ct) => Ok(await service.MoveRoomAsync(id, detailId, r.RoomId, r.Reason, ct));
    [HttpPost("bookings/{id:long}/invoice:prepare"), RequirePermission("BILLING_WRITE")]
    public async Task<IActionResult> Prepare(long id, CheckOutRequest r, CancellationToken ct) => Ok(await service.PrepareInvoiceAsync(id, r, ct));
    [HttpGet("bookings/{id:long}/history")]
    public async Task<IActionResult> History(long id, CancellationToken ct)
    {
        await actor.BookingAsync(id, "BOOKING_READ", ct); return Ok(await db.Set<LichSuTrangThaiDatPhong>().AsNoTracking().Where(x => x.DatPhongId == id).OrderBy(x => x.Id).ToListAsync(ct));
    }
    [HttpGet("bookings/{id:long}/guests"), RequirePermission("BOOKING_READ")]
    public async Task<IActionResult> Guests(long id, CancellationToken ct) => Ok(await db.Set<KhachLuuTru>().AsNoTracking().Where(x => db.ChiTietDatPhongs.Any(d => d.Id == x.ChiTietDatPhongId && d.DatPhongId == id)).ToListAsync(ct));
    [HttpPost("bookings/{id:long}/rooms/{detailId:long}/guests"), RequirePermission("BOOKING_WRITE")]
    public async Task<IActionResult> Guest(long id, long detailId, StayGuestRequest r, CancellationToken ct)
    {
        var guest = await service.AddGuestAsync(id, new BookingGuest(detailId, r.FullName, r.BirthDate,
            r.Gender, r.DocumentType, r.DocumentNumber, r.Nationality, r.Representative), ct);
        return Created($"/v1/bookings/{id}/guests/{guest.Id}", guest);
    }
    [HttpPost("bookings/{id:long}/change-requests")]
    public async Task<IActionResult> ChangeRequest(long id, BookingChangeRequest r, CancellationToken ct)
    {
        var booking = await actor.BookingAsync(id, "BOOKING_WRITE", ct); Input.OneOf(r.Type, "DOI_NGAY", "DOI_PHONG", "GIA_HAN");
        if (booking.TrangThai is not ("HOLD" or "BOOKED" or "DANG_O")) throw new InvalidArgumentException("Booking cannot be changed.");
        if (r.Type == "DOI_PHONG" && (!r.NewRoomId.HasValue || !await db.Phongs.AnyAsync(x => x.Id == r.NewRoomId, ct))) throw new InvalidArgumentException("A valid new room is required.");
        if (r.Type != "DOI_PHONG" && (!r.NewCheckOut.HasValue || r.NewCheckOut <= (r.NewCheckIn ?? booking.DuKienNhan))) throw new InvalidArgumentException("New stay dates are invalid.");
        var x = new YeuCauThayDoiDatPhong { DatPhongId = id, KhachHangId = booking.KhachHangId, LoaiYeuCau = r.Type, NoiDung = r.Content, DuKienNhanMoi = r.NewCheckIn, DuKienTraMoi = r.NewCheckOut, PhongMoiId = r.NewRoomId, CreatedAt = DateTime.UtcNow };
        db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/booking-change-requests/{x.Id}", x);
    }
    [HttpGet("booking-change-requests")]
    public async Task<IActionResult> Changes([FromQuery] ListQuery q, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BOOKING_READ", ct);
        return Ok(await db.Set<YeuCauThayDoiDatPhong>().AsNoTracking().Where(x => (staff || db.KhachHangs.Any(c => c.Id == x.KhachHangId && c.TaiKhoanId == actor.AccountId)) && (q.Status == null || x.TrangThai == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpPost("booking-change-requests/{id:long}:resolve"), RequirePermission("BOOKING_WRITE")]
    public async Task<IActionResult> Resolve(long id, ResolveChangeRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var x = await db.Set<YeuCauThayDoiDatPhong>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Change request was not found.");
        if (x.TrangThai != "CHO_XU_LY") throw new InvalidArgumentException("Request is already resolved.");
        var b = await db.DatPhongs.SingleAsync(b => b.Id == x.DatPhongId, ct);
        if (r.Completed && ((x.LoaiYeuCau != "DOI_PHONG" && (b.DuKienTra != x.DuKienTraMoi || (x.DuKienNhanMoi.HasValue && b.DuKienNhan != x.DuKienNhanMoi))) || (x.LoaiYeuCau == "DOI_PHONG" && !await db.ChiTietDatPhongs.AnyAsync(d => d.DatPhongId == b.Id && d.PhongId == x.PhongMoiId, ct))))
            throw new InvalidArgumentException("Apply the requested dates/room using the booking action before marking it completed.");
        x.TrangThai = r.Completed ? "HOAN_THANH" : "TU_CHOI"; x.KetQua = r.Result; x.NhanVienXuLyId = await actor.EmployeeIdAsync(ct); x.XuLyLuc = DateTime.UtcNow;
        await notifications.BookingAsync(b, actor.AccountId, b.TrangThai, r.Result, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(x);
    }
    [HttpGet("cancellation-requests")]
    public async Task<IActionResult> Cancellations([FromQuery] ListQuery q, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BOOKING_READ", ct); return Ok(await db.YeuCauHuyDatPhongs.AsNoTracking().Where(x => (staff || db.KhachHangs.Any(c => c.Id == x.KhachHangId && c.TaiKhoanId == actor.AccountId)) && (q.Status == null || x.TrangThai == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
    [HttpPost("cancellation-requests/{id:long}:reject"), RequirePermission("CANCELLATION_APPROVE")]
    public async Task<IActionResult> Reject(long id, ReasonRequest r, CancellationToken ct)
        => Ok(await service.RejectCancellationAsync(id, r.Reason, ct));
}
