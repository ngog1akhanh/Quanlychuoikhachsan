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
public sealed record HousekeepingCreateRequest(long RoomId, long? BookingId, string Type, long? AssigneeId, string Priority, DateTime? PlannedStart, [param: StringLength(1000)] string? Note);
public sealed record AssignTaskRequest(long EmployeeId);
public sealed record ResolveConditionRequest(string Status, decimal ActualCost, bool ReleaseMaintenance);

[ApiController, Authorize, Route("v1")]
public sealed class HousekeepingOperationsController(HotelDbContext db, CurrentActor actor) : ControllerBase
{
    [HttpPost("housekeeping-tasks"), RequirePermission("HOUSEKEEPING_WRITE")]
    public async Task<IActionResult> Create(HousekeepingCreateRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Type, "DON_CHECKOUT", "DON_HANG_NGAY", "KIEM_TRA"); Input.OneOf(r.Priority, "THAP", "BINH_THUONG", "CAO", "KHAN_CAP");
        if (!await db.Phongs.AnyAsync(x => x.Id == r.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        if (r.BookingId.HasValue && !await db.ChiTietDatPhongs.AnyAsync(x => x.DatPhongId == r.BookingId && x.PhongId == r.RoomId, ct)) throw new InvalidArgumentException("Room does not belong to this booking.");
        if (r.AssigneeId.HasValue) await ValidateAssignee(r.AssigneeId.Value, ct);
        var x = new CongViecBuongPhong { MaCongViec = $"HK{Guid.NewGuid():N}", PhongId = r.RoomId, DatPhongId = r.BookingId, LoaiCongViec = r.Type, NhanVienDuocGiaoId = r.AssigneeId,
            NhanVienPhanCongId = await actor.EmployeeIdAsync(ct), MucDoUuTien = r.Priority, BatDauDuKien = r.PlannedStart, GhiChu = r.Note };
        db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/housekeeping-tasks/{x.Id}", x);
    }
    [HttpGet("housekeeping-tasks/{id:long}"), RequirePermission("HOUSEKEEPING_READ")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await db.CongViecBuongPhongs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Task was not found."));
    [HttpPut("housekeeping-tasks/{id:long}/assignee"), RequirePermission("HOUSEKEEPING_ASSIGN")]
    public async Task<IActionResult> Assign(long id, AssignTaskRequest r, CancellationToken ct)
    {
        await ValidateAssignee(r.EmployeeId, ct); var x = await db.CongViecBuongPhongs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Task was not found.");
        if (x.TrangThai == "HOAN_THANH") throw new InvalidArgumentException("Completed task cannot be assigned.");
        x.NhanVienDuocGiaoId = r.EmployeeId; x.NhanVienPhanCongId = await actor.EmployeeIdAsync(ct); await db.SaveChangesAsync(ct); return Ok(x);
    }
    private async Task ValidateAssignee(long id, CancellationToken ct)
    {
        if (!await db.NhanViens.AnyAsync(x => x.Id == id && x.TrangThai == "DANG_LAM" && x.TaiKhoan.TrangThai == "HOAT_DONG", ct)) throw new ResourceNotFoundException("Active employee was not found.");
    }
    [HttpPatch("housekeeping-tasks/{id:long}/status"), RequirePermission("HOUSEKEEPING_WRITE")]
    public async Task<IActionResult> Status(long id, StatusRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.CongViecBuongPhongs.FromSqlInterpolated($"SELECT * FROM cong_viec_buong_phong WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var x = await db.CongViecBuongPhongs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Task was not found.");
        var employee = await actor.EmployeeIdAsync(ct);
        if (x.NhanVienDuocGiaoId.HasValue && x.NhanVienDuocGiaoId != employee && !await actor.HasPermissionAsync("HOUSEKEEPING_ASSIGN", ct)) throw new PermissionDeniedException("Task belongs to another employee.");
        if (!((x.TrangThai == "CHO_XU_LY" && r.Status == "DANG_THUC_HIEN") || (x.TrangThai == "DANG_THUC_HIEN" && r.Status == "HOAN_THANH"))) throw new InvalidArgumentException("Invalid task state transition.");
        var room = await db.Phongs.SingleAsync(p => p.Id == x.PhongId, ct);
        if (r.Status == "DANG_THUC_HIEN")
        {
            x.NhanVienDuocGiaoId ??= employee; x.BatDauThucTe = DateTime.UtcNow;
            if (x.LoaiCongViec == "DON_CHECKOUT" && room.TrangThai == "DANG_O") throw new InvalidArgumentException("Guest has not checked out.");
            if (x.LoaiCongViec != "KIEM_TRA" && room.TrangThai is "CAN_DON" or "SAN_SANG") room.TrangThai = "DANG_DON";
        }
        else
        {
            x.HoanThanhLuc = DateTime.UtcNow;
            var occupied = await db.ChiTietDatPhongs.AnyAsync(d => d.PhongId == room.Id && d.TrangThai == "DANG_O", ct);
            var otherCleaning = await db.CongViecBuongPhongs.AnyAsync(t => t.Id != id && t.PhongId == room.Id && t.LoaiCongViec != "KIEM_TRA" && t.TrangThai != "HOAN_THANH", ct);
            if (x.LoaiCongViec != "KIEM_TRA" && !occupied && !otherCleaning && room.TrangThai is "DANG_DON" or "CAN_DON") room.TrangThai = "SAN_SANG";
        }
        x.TrangThai = r.Status; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(x);
    }
    [HttpGet("room-conditions"), RequirePermission("HOUSEKEEPING_READ")]
    public async Task<IActionResult> Conditions([FromQuery] ListQuery q, long? roomId, long? bookingId, CancellationToken ct) => Ok(await db.TinhTrangPhongs.AsNoTracking().Where(x => (!roomId.HasValue || x.PhongId == roomId) && (!bookingId.HasValue || x.DatPhongId == bookingId) && (q.Status == null || x.TrangThaiXuLy == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    [HttpPatch("room-conditions/{id:long}"), RequirePermission("HOUSEKEEPING_WRITE")]
    public async Task<IActionResult> Resolve(long id, ResolveConditionRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "DANG_XU_LY", "DA_XU_LY"); Input.Money(r.ActualCost, "actual_cost");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var x = await db.TinhTrangPhongs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Condition was not found.");
        if (x.TrangThaiXuLy == "DA_XU_LY") throw new InvalidArgumentException("Condition is already resolved.");
        if (x.DoKhachGayRa && x.DatPhongId.HasValue && await db.HoaDonPhongs.AnyAsync(i => i.DatPhongId == x.DatPhongId && i.TrangThai != "NHAP" && i.TrangThai != "DA_HUY", ct)) throw new InvalidArgumentException("Customer charges are already invoiced.");
        x.TrangThaiXuLy = r.Status; x.ChiPhiThucTe = r.ActualCost; if (r.Status == "DA_XU_LY") x.XuLyXongLuc = DateTime.UtcNow;
        if (r.ReleaseMaintenance)
        {
            if (r.Status != "DA_XU_LY" || await db.TinhTrangPhongs.AnyAsync(c => c.PhongId == x.PhongId && c.Id != id && c.MucDo == "NGHIEM_TRONG" && c.TrangThaiXuLy != "DA_XU_LY", ct)) throw new InvalidArgumentException("Other severe incidents remain open.");
            var room = await db.Phongs.SingleAsync(p => p.Id == x.PhongId, ct); if (room.TrangThai == "BAO_TRI") room.TrangThai = "CAN_DON";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(x);
    }
}
