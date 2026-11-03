using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Accounts;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

public sealed record ForgotPasswordRequest([param: Required, EmailAddress, StringLength(150)] string Email);
public sealed record ResetPasswordRequest([param: Required, StringLength(128)] string Token, [param: Required, MinLength(8), StringLength(128)] string Password);
public sealed record ChangePasswordRequest([param: Required] string CurrentPassword, [param: Required, MinLength(8), StringLength(128)] string NewPassword);
public sealed record ProfileRequest([param: Required, StringLength(120)] string FullName, DateOnly? BirthDate,
    [param: StringLength(20)] string? Gender, [param: StringLength(40)] string? IdentityNumber, [param: StringLength(255)] string? Address,
    [param: Phone, StringLength(20)] string? PhoneNumber);

[ApiController, Authorize, Route("v1")]
public sealed class MyAccountController(HotelDbContext db, CurrentActor actor, IPasswordHasher<TaiKhoan> hasher, PasswordRecovery recovery) : ControllerBase
{
    [AllowAnonymous, HttpPost("auth/forgot-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest r, CancellationToken ct) { await recovery.RequestAsync(r.Email, ct); return Accepted(new { Message = "If the account is eligible, a recovery email will be sent." }); }
    [AllowAnonymous, HttpPost("auth/reset-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(ResetPasswordRequest r, CancellationToken ct) { await recovery.ResetAsync(r.Token, r.Password, ct); return NoContent(); }
    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirstValue("jti") ?? throw new UnauthenticatedException();
        var expiry = long.TryParse(User.FindFirstValue("exp"), out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : DateTime.UtcNow.AddHours(1);
        db.Add(new MaXacThuc { TaiKhoanId = actor.AccountId, Loai = "JWT_REVOKED", MaHash = SessionValidation.Hash(jti), HetHanLuc = expiry, DaSuDung = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("me")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var account = await db.TaiKhoans.AsNoTracking().Include(x => x.KhachHang).Include(x => x.NhanVien)
            .SingleOrDefaultAsync(x => x.Id == actor.AccountId, ct) ?? throw new UnauthenticatedException();
        var roles = await db.TaiKhoanVaiTros.AsNoTracking()
            .Where(x => x.TaiKhoanId == actor.AccountId && x.VaiTro.TrangThai == "HOAT_DONG")
            .Select(x => new { Code = x.VaiTro.MaVaiTro, Name = x.VaiTro.TenVaiTro }).ToListAsync(ct);
        var permissions = await db.TaiKhoanVaiTros.AsNoTracking()
            .Where(x => x.TaiKhoanId == actor.AccountId && x.VaiTro.TrangThai == "HOAT_DONG")
            .SelectMany(x => x.VaiTro.VaiTroQuyens.Select(p => p.Quyen.MaQuyen)).Distinct().OrderBy(x => x).ToListAsync(ct);
        var customer = account.KhachHang is null ? null : AccountService.ToCustomerResponse(account.KhachHang);
        // Trường khách hàng vẫn ở cấp gốc để các client đang dùng /me tiếp tục đọc được.
        return Ok(new
        {
            customer?.Name,
            customer?.Id,
            customer?.CustomerCode,
            customer?.FullName,
            Email = customer?.Email ?? account.Email,
            customer?.PhoneNumber,
            customer?.BirthDate,
            customer?.Gender,
            customer?.IdentityNumber,
            customer?.Address,
            customer?.CreateTime,
            customer?.UpdateTime,
            AccountId = account.Id,
            DisplayName = account.NhanVien?.HoTen ?? customer?.FullName ?? account.Email ?? account.SoDienThoai,
            AccountType = account.LoaiTaiKhoan,
            StaffId = account.NhanVien?.Id,
            StaffCode = account.NhanVien?.MaNhanVien,
            Roles = roles,
            Permissions = permissions
        });
    }
    [HttpPut("me")]
    public async Task<IActionResult> Profile(ProfileRequest r, CancellationToken ct)
    {
        var x = await db.KhachHangs.Include(x => x.TaiKhoan).SingleOrDefaultAsync(x => x.TaiKhoanId == actor.AccountId, ct) ?? throw new ResourceNotFoundException("Customer profile was not found.");
        if (r.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new InvalidArgumentException("Birth date cannot be in the future.");
        if (!string.IsNullOrWhiteSpace(r.PhoneNumber) && await db.TaiKhoans.AnyAsync(a => a.Id != actor.AccountId && a.SoDienThoai == r.PhoneNumber, ct)) throw new AlreadyExistsException("Phone is already registered.");
        if (string.IsNullOrWhiteSpace(r.PhoneNumber) && x.TaiKhoan!.Email is null) throw new InvalidArgumentException("At least one login contact must remain.");
        x.HoTen = Input.Text(r.FullName, 120, "full_name"); x.NgaySinh = r.BirthDate; x.GioiTinh = r.Gender; x.CccdHoChieu = r.IdentityNumber; x.DiaChi = r.Address; x.SoDienThoai = r.PhoneNumber; x.UpdatedAt = DateTime.UtcNow;
        x.TaiKhoan!.SoDienThoai = r.PhoneNumber; await db.SaveChangesAsync(ct); return Ok(AccountService.ToCustomerResponse(x));
    }
    [HttpPost("me/change-password")]
    public async Task<IActionResult> Password(ChangePasswordRequest r, CancellationToken ct)
    {
        var x = await db.TaiKhoans.SingleAsync(x => x.Id == actor.AccountId, ct);
        if (hasher.VerifyHashedPassword(x, x.MatKhauHash, r.CurrentPassword) == PasswordVerificationResult.Failed) throw new InvalidArgumentException("Current password is incorrect.");
        x.MatKhauHash = hasher.HashPassword(x, r.NewPassword); x.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("me/bookings")]
    public async Task<IActionResult> Bookings([FromQuery] ListQuery q, CancellationToken ct) => Ok(await db.DatPhongs.AsNoTracking().Where(x => x.KhachHang.TaiKhoanId == actor.AccountId && (q.Status == null || x.TrangThai == q.Status))
        .OrderByDescending(x => x.Id).Select(x => new { x.Id, Code = x.MaDatPhong, Status = x.TrangThai, CheckIn = x.DuKienNhan, CheckOut = x.DuKienTra, Total = x.TongTienDuKien }).PageAsync(q, ct));
}
