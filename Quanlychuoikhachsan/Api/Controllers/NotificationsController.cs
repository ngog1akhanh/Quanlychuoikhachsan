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
/// <summary>Gửi thông báo nội bộ: chọn người nhận theo tài khoản (AccountIds) và/hoặc theo vai trò (RoleIds).</summary>
public sealed record SendNotificationRequest([param: Required, StringLength(255)] string Title, [param: Required, StringLength(3000)] string Content,
    [param: MaxLength(500)] IReadOnlyList<long>? AccountIds = null, long? BookingId = null,
    [param: MaxLength(50)] IReadOnlyList<long>? RoleIds = null, bool Important = false);

[ApiController, Authorize, Route("v1/notifications")]
public sealed class NotificationsController(HotelDbContext db, CurrentActor actor, NotificationWriter writer) : ControllerBase
{
    /// <summary>
    /// Hộp thư của người đang đăng nhập. group: BOOKING (đặt phòng), PAYMENT (thanh toán), OPERATIONS (vận hành / nội bộ).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListQuery q, bool unreadOnly, string? group, CancellationToken ct)
    {
        var g = string.IsNullOrWhiteSpace(group) ? null : group.Trim().ToUpperInvariant();
        return Ok(await
            (from delivery in db.Set<ThongBaoTaiKhoan>().AsNoTracking()
             join notification in db.Set<ThongBao>() on delivery.ThongBaoId equals notification.Id
             where delivery.TaiKhoanId == actor.AccountId && delivery.KenhGui == "APP" && (!unreadOnly || !delivery.DaDoc)
                && (g == null
                    || (g == "BOOKING" && notification.LoaiThongBao.StartsWith("BOOKING"))
                    || (g == "PAYMENT" && notification.LoaiThongBao == "PAYMENT")
                    || (g == "OPERATIONS" && !notification.LoaiThongBao.StartsWith("BOOKING") && notification.LoaiThongBao != "PAYMENT"))
             orderby notification.Id descending
             select new { Notification = notification, Read = delivery.DaDoc, ReadAt = delivery.DaDocLuc }).PageAsync(q, ct));
    }

    /// <summary>Số thông báo chưa đọc (chuông trên app gọi định kỳ).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var unread = db.Set<ThongBaoTaiKhoan>().AsNoTracking().Where(x => x.TaiKhoanId == actor.AccountId && x.KenhGui == "APP" && !x.DaDoc);
        var count = await unread.CountAsync(ct);
        var latestId = await unread.Select(x => (long?)x.ThongBaoId).MaxAsync(ct);
        return Ok(new { Count = count, LatestId = latestId });
    }

    [HttpPost("{id:long}:read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        var rows = await db.Set<ThongBaoTaiKhoan>().Where(x => x.ThongBaoId == id && x.TaiKhoanId == actor.AccountId).ToListAsync(ct);
        if (rows.Count == 0) throw new ResourceNotFoundException("Notification was not found.");
        foreach (var x in rows) { x.DaDoc = true; x.DaDocLuc ??= DateTime.UtcNow; } await db.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Đánh dấu tất cả thông báo của người đang đăng nhập là đã đọc.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var rows = await db.Set<ThongBaoTaiKhoan>().Where(x => x.TaiKhoanId == actor.AccountId && !x.DaDoc).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var x in rows) { x.DaDoc = true; x.DaDocLuc ??= now; }
        await db.SaveChangesAsync(ct);
        return Ok(new { Updated = rows.Count });
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

    /// <summary>Danh sách vai trò và nhân viên đang làm để chọn người nhận khi gửi thông báo.</summary>
    [HttpGet("recipients"), RequirePermission("NOTIFICATION_WRITE")]
    public async Task<IActionResult> Recipients(CancellationToken ct)
    {
        var roles = await db.Set<VaiTro>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").OrderBy(x => x.TenVaiTro)
            .Select(x => new { x.Id, Name = x.TenVaiTro, Members = x.TaiKhoanVaiTros.Count(a => a.TaiKhoan.TrangThai == "HOAT_DONG") }).ToListAsync(ct);
        var staff = await db.NhanViens.AsNoTracking().Where(x => x.TrangThai == "DANG_LAM" && x.TaiKhoan.TrangThai == "HOAT_DONG").OrderBy(x => x.HoTen)
            .Select(x => new { AccountId = x.TaiKhoanId, Name = x.HoTen, Position = x.ChucVu.TenChucVu,
                Roles = x.TaiKhoan.TaiKhoanVaiTros.Select(r => r.VaiTro.TenVaiTro).ToList() }).ToListAsync(ct);
        return Ok(new { Roles = roles, Staff = staff });
    }

    [HttpPost, RequirePermission("NOTIFICATION_WRITE")]
    public async Task<IActionResult> Send(SendNotificationRequest r, CancellationToken ct)
    {
        var accountIds = (r.AccountIds ?? []).Distinct().ToArray();
        if (accountIds.Length > 0 && await db.TaiKhoans.CountAsync(x => accountIds.Contains(x.Id) && x.TrangThai == "HOAT_DONG", ct) != accountIds.Length)
            throw new InvalidArgumentException("One or more active recipients were not found.");
        var recipients = accountIds.ToList();
        var roleIds = (r.RoleIds ?? []).Distinct().ToArray();
        if (roleIds.Length > 0)
            recipients.AddRange(await db.TaiKhoanVaiTros.Where(x => roleIds.Contains(x.VaiTroId) && x.TaiKhoan.TrangThai == "HOAT_DONG" && x.VaiTro.TrangThai == "HOAT_DONG")
                .Select(x => x.TaiKhoanId).ToListAsync(ct));
        recipients = recipients.Distinct().ToList();
        if (recipients.Count == 0) throw new InvalidArgumentException("Chọn ít nhất một người nhận (vai trò không có tài khoản đang hoạt động).");
        var booking = r.BookingId.HasValue ? await actor.BookingAsync(r.BookingId.Value, "BOOKING_READ", ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await writer.SendAsync(booking is null ? "OPERATIONS" : "BOOKING", r.Title, r.Content, recipients, actor.AccountId, booking, ct, r.Important ? "QUAN_TRONG" : "THONG_THUONG");
        await tx.CommitAsync(ct);
        return Ok(new { Recipients = recipients.Count });
    }
}
