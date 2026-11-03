using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record NotificationSettingsRequest(bool Booking, bool Payment, bool Operations, bool Email, bool Push);
public sealed record SendNotificationRequest([param: Required, StringLength(255)] string Title, [param: Required, StringLength(3000)] string Content,
    [param: Required, MinLength(1), MaxLength(500)] IReadOnlyList<long> AccountIds, long? BookingId = null);

[ApiController, Authorize, Route("v1/notifications")]
public sealed class NotificationsController(HotelDbContext db, CurrentActor actor, NotificationWriter writer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListQuery q, bool unreadOnly, CancellationToken ct) => Ok(await
        (from delivery in db.Set<ThongBaoTaiKhoan>().AsNoTracking()
         join notification in db.Set<ThongBao>() on delivery.ThongBaoId equals notification.Id
         where delivery.TaiKhoanId == actor.AccountId && delivery.KenhGui == "APP" && (!unreadOnly || !delivery.DaDoc)
         orderby notification.Id descending
         select new { Notification = notification, Read = delivery.DaDoc, ReadAt = delivery.DaDocLuc }).PageAsync(q, ct));
    [HttpPost("{id:long}:read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        var rows = await db.Set<ThongBaoTaiKhoan>().Where(x => x.ThongBaoId == id && x.TaiKhoanId == actor.AccountId).ToListAsync(ct);
        if (rows.Count == 0) throw new ResourceNotFoundException("Notification was not found.");
        foreach (var x in rows) { x.DaDoc = true; x.DaDocLuc ??= DateTime.UtcNow; } await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await db.Set<CaiDatThongBao>().AsNoTracking().SingleOrDefaultAsync(x => x.TaiKhoanId == actor.AccountId, ct) ?? new CaiDatThongBao { TaiKhoanId = actor.AccountId });
    [HttpPut("settings")]
    public async Task<IActionResult> Settings(NotificationSettingsRequest r, CancellationToken ct)
    {
        var x = await db.Set<CaiDatThongBao>().SingleOrDefaultAsync(x => x.TaiKhoanId == actor.AccountId, ct);
        if (x is null) { x = new CaiDatThongBao { TaiKhoanId = actor.AccountId }; db.Add(x); }
        x.NhanBooking = r.Booking; x.NhanThanhToan = r.Payment; x.NhanVanHanh = r.Operations; x.ChoPhepEmail = r.Email; x.ChoPhepPush = r.Push;
        await db.SaveChangesAsync(ct); return Ok(x);
    }
    [HttpPost, RequirePermission("NOTIFICATION_WRITE")]
    public async Task<IActionResult> Send(SendNotificationRequest r, CancellationToken ct)
    {
        var ids = r.AccountIds.Distinct().ToArray();
        if (await db.TaiKhoans.CountAsync(x => ids.Contains(x.Id) && x.TrangThai == "HOAT_DONG", ct) != ids.Length) throw new InvalidArgumentException("One or more active recipients were not found.");
        var booking = r.BookingId.HasValue ? await actor.BookingAsync(r.BookingId.Value, "BOOKING_READ", ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await writer.SendAsync(booking is null ? "OPERATIONS" : "BOOKING", r.Title, r.Content, ids, actor.AccountId, booking, ct); await tx.CommitAsync(ct); return NoContent();
    }
}
