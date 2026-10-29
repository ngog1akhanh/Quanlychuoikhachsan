using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Accounts;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record StaffCreateRequest([param: Required, StringLength(30)] string Code, [param: Required, StringLength(120)] string FullName,
    long PositionId, [param: Required, EmailAddress, StringLength(150)] string Email, [param: Phone, StringLength(20)] string? Phone,
    [param: Required, MinLength(8), StringLength(128)] string Password, DateOnly? StartDate);
public sealed record StaffUpdateRequest([param: Required, StringLength(120)] string FullName, long PositionId,
    [param: StringLength(255)] string? Address, DateOnly? BirthDate, string Status);
public sealed record RoleWriteRequest([param: Required, StringLength(50)] string Code, [param: Required, StringLength(100)] string Name,
    [param: StringLength(500)] string? Description, string Status = "HOAT_DONG");
public sealed record IdsRequest([param: Required, MaxLength(100)] IReadOnlyList<long> Ids);
public sealed record AdminPasswordRequest([param: Required, MinLength(8), StringLength(128)] string Password);

[ApiController, Authorize, Route("v1")]
public sealed class AdministrationController(HotelDbContext db, CurrentActor actor, IPasswordHasher<TaiKhoan> hasher) : ControllerBase
{
    [HttpPost("customers"), RequirePermission("CUSTOMER_WRITE")]
    public async Task<IActionResult> CreateCustomer(ProfileRequest r, CancellationToken ct)
    {
        var x = new KhachHang { MaKhachHang = $"KH{Guid.NewGuid():N}"[..30], HoTen = Input.Text(r.FullName, 120, "full_name"), NgaySinh = r.BirthDate,
            GioiTinh = r.Gender, CccdHoChieu = r.IdentityNumber, DiaChi = r.Address, SoDienThoai = r.PhoneNumber, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/customers/{x.Id}", AccountService.ToCustomerResponse(x));
    }
    [HttpGet("customers/search"), RequirePermission("CUSTOMER_READ")]
    public async Task<IActionResult> SearchCustomers([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.KhachHangs.AsNoTracking()
        .Where(x => q.Search == null || x.HoTen.Contains(q.Search) || (x.SoDienThoai != null && x.SoDienThoai.Contains(q.Search)) || x.MaKhachHang.Contains(q.Search))
        .OrderBy(x => x.Id).Select(x => new { x.Id, x.MaKhachHang, x.HoTen, x.SoDienThoai, x.Email }).PageAsync(q, ct));
    [HttpGet("customers/{id:long}/history"), RequirePermission("CUSTOMER_READ")]
    public async Task<IActionResult> CustomerHistory(long id, [FromQuery] ListQuery q, CancellationToken ct)
    {
        if (!await db.KhachHangs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Customer was not found.");
        return Ok(await db.DatPhongs.AsNoTracking().Where(x => x.KhachHangId == id).OrderByDescending(x => x.Id).Select(x => new { x.Id, x.MaDatPhong, x.DuKienNhan, x.DuKienTra, x.TrangThai, x.TongTienDuKien }).PageAsync(q, ct));
    }
    [HttpGet("positions"), RequirePermission("STAFF_READ")]
    public async Task<IActionResult> Positions(CancellationToken ct) => Ok(await db.ChucVus.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, Code = x.MaChucVu, Name = x.TenChucVu, x.TrangThai }).ToListAsync(ct));
    [HttpPost("positions"), RequirePermission("STAFF_WRITE")]
    public async Task<IActionResult> Position(RoleWriteRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Text(r.Code, 30, "code");
        var x = new ChucVu { MaChucVu = r.Code, TenChucVu = r.Name, MoTa = r.Description, TrangThai = r.Status }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/positions/{x.Id}", new { x.Id });
    }
    [HttpPost("staff"), RequirePermission("STAFF_WRITE")]
    public async Task<IActionResult> Staff(StaffCreateRequest r, CancellationToken ct)
    {
        if (!await db.ChucVus.AnyAsync(x => x.Id == r.PositionId && x.TrangThai == "HOAT_DONG", ct)) throw new ResourceNotFoundException("Position was not found.");
        if (await db.TaiKhoans.AnyAsync(x => x.Email == r.Email || (r.Phone != null && x.SoDienThoai == r.Phone), ct)) throw new AlreadyExistsException("Login contact is already registered.");
        var account = new TaiKhoan { Email = r.Email.Trim().ToLowerInvariant(), SoDienThoai = r.Phone, LoaiTaiKhoan = "NHAN_VIEN", TrangThai = "HOAT_DONG", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        account.MatKhauHash = hasher.HashPassword(account, r.Password);
        var x = new NhanVien { MaNhanVien = r.Code, HoTen = r.FullName, ChucVuId = r.PositionId, TaiKhoan = account, Email = account.Email, SoDienThoai = r.Phone, NgayVaoLam = r.StartDate, TrangThai = "DANG_LAM", CreatedAt = DateTime.UtcNow };
        db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/staff/{x.Id}", new { x.Id, AccountId = account.Id, x.HoTen, x.TrangThai });
    }
    [HttpPut("staff/{id:long}"), RequirePermission("STAFF_WRITE")]
    public async Task<IActionResult> Staff(long id, StaffUpdateRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "DANG_LAM", "NGHI_VIEC", "TAM_NGHI");
        if (!await db.ChucVus.AnyAsync(x => x.Id == r.PositionId, ct)) throw new ResourceNotFoundException("Position was not found.");
        var x = await db.NhanViens.Include(x => x.TaiKhoan).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Employee was not found.");
        if (x.TaiKhoanId == actor.AccountId && r.Status != "DANG_LAM") throw new InvalidArgumentException("Cannot deactivate your own employee profile.");
        x.HoTen = r.FullName; x.ChucVuId = r.PositionId; x.DiaChi = r.Address; x.NgaySinh = r.BirthDate; x.TrangThai = r.Status;
        if (r.Status != "DANG_LAM")
        {
            x.TaiKhoan.TrangThai = "KHOA";
            db.Add(new MaXacThuc { TaiKhoanId = x.TaiKhoanId, Loai = "SESSION_VERSION", MaHash = Guid.NewGuid().ToString("N"), HetHanLuc = DateTime.UtcNow.AddYears(100), CreatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(ct); return Ok(new { x.Id, x.HoTen, x.TrangThai });
    }
    [HttpPatch("accounts/{id:long}/status"), RequirePermission("ACCOUNT_MANAGE")]
    public async Task<IActionResult> AccountStatus(long id, StatusRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "KHOA"); if (id == actor.AccountId) throw new InvalidArgumentException("Cannot change your own account status.");
        var x = await db.TaiKhoans.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Account was not found."); x.TrangThai = r.Status; x.UpdatedAt = DateTime.UtcNow;
        // Revoke sessions without replacing the password when temporarily locking an account.
        if (r.Status == "KHOA") db.Add(new MaXacThuc { TaiKhoanId = id, Loai = "SESSION_VERSION", MaHash = Guid.NewGuid().ToString("N"), HetHanLuc = DateTime.UtcNow.AddYears(100), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPost("accounts/{id:long}:reset-password"), RequirePermission("ACCOUNT_MANAGE")]
    public async Task<IActionResult> ResetPassword(long id, AdminPasswordRequest r, CancellationToken ct)
    {
        var x = await db.TaiKhoans.SingleOrDefaultAsync(x => x.Id == id && x.LoaiTaiKhoan == "NHAN_VIEN", ct) ?? throw new ResourceNotFoundException("Staff account was not found.");
        x.MatKhauHash = hasher.HashPassword(x, r.Password); x.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("permissions"), RequirePermission("ROLE_READ")]
    public async Task<IActionResult> Permissions(CancellationToken ct) => Ok(await db.Quyens.AsNoTracking().OrderBy(x => x.MaQuyen).Select(x => new { x.Id, x.MaQuyen, x.TenQuyen, x.NhomQuyen }).ToListAsync(ct));
    [HttpPost("roles"), RequirePermission("ROLE_WRITE")]
    public async Task<IActionResult> Role(RoleWriteRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); var x = new VaiTro { MaVaiTro = r.Code, TenVaiTro = r.Name, MoTa = r.Description, TrangThai = r.Status };
        db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/roles/{x.Id}", new { x.Id, x.MaVaiTro, x.TenVaiTro });
    }
    [HttpPut("roles/{id:long}"), RequirePermission("ROLE_WRITE")]
    public async Task<IActionResult> Role(long id, RoleWriteRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        var x = await db.VaiTros.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Role was not found.");
        if (x.MaVaiTro == "ADMIN" && (r.Code != "ADMIN" || r.Status != "HOAT_DONG")) throw new InvalidArgumentException("The ADMIN role must remain active.");
        x.MaVaiTro = r.Code; x.TenVaiTro = r.Name; x.MoTa = r.Description; x.TrangThai = r.Status; await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("roles/{id:long}/permissions"), RequirePermission("ROLE_READ")]
    public async Task<IActionResult> RolePermissions(long id, CancellationToken ct) => Ok(await db.VaiTroQuyens.Where(x => x.VaiTroId == id).Select(x => new { x.QuyenId, x.Quyen.MaQuyen }).ToListAsync(ct));
    [HttpPut("roles/{id:long}/permissions"), RequirePermission("ROLE_WRITE")]
    public async Task<IActionResult> RolePermissions(long id, IdsRequest r, CancellationToken ct)
    {
        var role = await db.VaiTros.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Role was not found.");
        if (role.MaVaiTro == "ADMIN") throw new InvalidArgumentException("ADMIN permissions are managed by the permission seed script.");
        var ids = r.Ids.Distinct().ToArray(); if (await db.Quyens.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) throw new InvalidArgumentException("Unknown permission.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.VaiTroQuyens.RemoveRange(await db.VaiTroQuyens.Where(x => x.VaiTroId == id).ToListAsync(ct)); await db.SaveChangesAsync(ct);
        db.VaiTroQuyens.AddRange(ids.Select(p => new VaiTroQuyen { VaiTroId = id, QuyenId = p })); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent();
    }
    [HttpGet("accounts/{id:long}/roles"), RequirePermission("ROLE_READ")]
    public async Task<IActionResult> AccountRoles(long id, CancellationToken ct) => Ok(await db.TaiKhoanVaiTros.Where(x => x.TaiKhoanId == id).Select(x => new { x.VaiTroId, x.VaiTro.MaVaiTro }).ToListAsync(ct));
    [HttpPut("accounts/{id:long}/roles"), RequirePermission("ROLE_WRITE")]
    public async Task<IActionResult> AccountRoles(long id, IdsRequest r, CancellationToken ct)
    {
        if (id == actor.AccountId) throw new InvalidArgumentException("Cannot replace your own roles.");
        if (!await db.TaiKhoans.AnyAsync(x => x.Id == id && x.LoaiTaiKhoan == "NHAN_VIEN", ct)) throw new ResourceNotFoundException("Staff account was not found.");
        var ids = r.Ids.Distinct().ToArray(); if (await db.VaiTros.CountAsync(x => ids.Contains(x.Id) && x.TrangThai == "HOAT_DONG", ct) != ids.Length) throw new InvalidArgumentException("Unknown active role.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.TaiKhoanVaiTros.RemoveRange(await db.TaiKhoanVaiTros.Where(x => x.TaiKhoanId == id).ToListAsync(ct)); await db.SaveChangesAsync(ct);
        db.TaiKhoanVaiTros.AddRange(ids.Select(p => new TaiKhoanVaiTro { TaiKhoanId = id, VaiTroId = p })); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent();
    }
    [HttpGet("audit-logs"), RequirePermission("AUDIT_READ")]
    public async Task<IActionResult> Audit([FromQuery] ListQuery q, long? accountId, CancellationToken ct) => Ok(await db.NhatKyHeThongs.AsNoTracking().Where(x => (!accountId.HasValue || x.TaiKhoanId == accountId) && (q.Search == null || x.DoiTuong == q.Search)).OrderByDescending(x => x.Id).PageAsync(q, ct));
}
