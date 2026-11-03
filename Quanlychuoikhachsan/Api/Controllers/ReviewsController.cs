using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

public sealed record ReviewRequest(long BookingId, [param: Range(1, 5)] byte Rating, [param: Range(1, 5)] byte? RoomRating,
    [param: Range(1, 5)] byte? ServiceRating, [param: StringLength(3000)] string? Content);
public sealed record ReviewReplyRequest([param: Required, StringLength(3000)] string Reply);

[ApiController, Authorize, Route("v1/reviews")]
public sealed class ReviewsController(HotelDbContext db, CurrentActor actor) : ControllerBase
{
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> List([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<DanhGia>().AsNoTracking().Where(x => x.TrangThai == "HIEN_THI")
        .OrderByDescending(x => x.Id).Select(x => new { x.Id, Rating = x.DiemTong, RoomRating = x.DiemPhong, ServiceRating = x.DiemDichVu, Content = x.NoiDung, CreatedAt = x.NgayDanhGia, Reply = x.PhanHoiKhachSan, RepliedAt = x.NgayPhanHoi }).PageAsync(q, ct));
    [HttpGet("manage"), RequirePermission("REVIEW_MANAGE")]
    public async Task<IActionResult> Manage([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<DanhGia>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.Set<DanhGia>().AsNoTracking().Where(x => db.KhachHangs.Any(c => c.Id == x.KhachHangId && c.TaiKhoanId == actor.AccountId)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpPost]
    public async Task<IActionResult> Create(ReviewRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var booking = await db.DatPhongs.SingleOrDefaultAsync(x => x.Id == r.BookingId && x.KhachHang.TaiKhoanId == actor.AccountId, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
        if (booking.TrangThai != "DA_TRA") throw new InvalidArgumentException("Only completed stays can be reviewed.");
        if (await db.Set<DanhGia>().AnyAsync(x => x.DatPhongId == booking.Id, ct)) throw new AlreadyExistsException("This booking already has a review.");
        var row = new DanhGia { MaDanhGia = $"DG{Guid.NewGuid():N}", DatPhongId = booking.Id, KhachHangId = booking.KhachHangId, DiemTong = r.Rating, DiemPhong = r.RoomRating, DiemDichVu = r.ServiceRating, NoiDung = r.Content, NgayDanhGia = DateTime.UtcNow, TrangThai = "HIEN_THI" };
        db.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Created($"/v1/reviews/{row.Id}", row);
    }
    [HttpPut("{id:long}/reply"), RequirePermission("REVIEW_MANAGE")]
    public async Task<IActionResult> Reply(long id, ReviewReplyRequest r, CancellationToken ct)
    {
        var x = await db.Set<DanhGia>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Review was not found.");
        x.PhanHoiKhachSan = r.Reply; x.NhanVienPhanHoiId = await actor.EmployeeIdAsync(ct); x.NgayPhanHoi = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(x);
    }
    [HttpPatch("{id:long}/status"), RequirePermission("REVIEW_MANAGE")]
    public async Task<IActionResult> Visibility(long id, StatusRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HIEN_THI", "AN"); var x = await db.Set<DanhGia>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Review was not found.");
        x.TrangThai = r.Status; await db.SaveChangesAsync(ct); return Ok(x);
    }
}
