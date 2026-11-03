using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Accounts;

public interface IAccountService
{
    Task<CustomerResponse> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken);
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

/// <summary>Đăng ký khách hàng, kiểm tra mật khẩu và phát hành JWT.</summary>
public sealed class AccountService(
    HotelDbContext dbContext,
    IConfiguration configuration,
    IPasswordHasher<TaiKhoan> passwordHasher) : IAccountService
{
    // Tạo tài khoản và hồ sơ khách hàng trong cùng một transaction.
    public async Task<CustomerResponse> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var phone = Normalize(request.PhoneNumber);

        if (email is null && phone is null)
            throw new InvalidArgumentException("At least one of email or phone_number is required.");

        var duplicate = await dbContext.TaiKhoans.AnyAsync(x =>
            (email != null && x.Email == email) || (phone != null && x.SoDienThoai == phone), cancellationToken);
        if (duplicate)
            throw new AlreadyExistsException("The email or phone_number is already registered.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var account = new TaiKhoan
        {
            Email = email,
            SoDienThoai = phone,
            LoaiTaiKhoan = "KHACH_HANG",
            TrangThai = "HOAT_DONG",
            MatKhauHash = string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };
        account.MatKhauHash = passwordHasher.HashPassword(account, request.Password);

        var customer = new KhachHang
        {
            MaKhachHang = $"KH{Guid.NewGuid():N}"[..30],
            HoTen = request.FullName.Trim(),
            TaiKhoan = account,
            NgaySinh = request.BirthDate,
            GioiTinh = request.Gender,
            SoDienThoai = phone,
            Email = email,
            CccdHoChieu = request.IdentityNumber,
            DiaChi = request.Address,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.KhachHangs.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToCustomerResponse(customer);
    }

    // Xác thực email/số điện thoại và phát hành access token có thời hạn.
    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = Normalize(request.Identifier);
        var account = await dbContext.TaiKhoans
            .Include(x => x.KhachHang)
            .SingleOrDefaultAsync(x => x.Email == identifier || x.SoDienThoai == identifier, cancellationToken);

        if (account is null || account.TrangThai != "HOAT_DONG" ||
            passwordHasher.VerifyHashedPassword(account, account.MatKhauHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthenticatedException("The identifier or password is incorrect.");
        }

        account.LanDangNhapCuoi = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var expiresIn = 3600;
        var sessionVersion = await dbContext.Set<MaXacThuc>().Where(x => x.TaiKhoanId == account.Id && x.Loai == "SESSION_VERSION").Select(x => (long?)x.Id).MaxAsync(cancellationToken) ?? 0;
        var token = CreateToken(account, expiresIn, sessionVersion);
        return new AuthTokenResponse(token, "Bearer", expiresIn, account.KhachHang?.HoTen ?? account.Email ?? account.SoDienThoai!);
    }

    private string CreateToken(TaiKhoan account, int expiresIn, long sessionVersion)
    {
        var key = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("credential_stamp", Infrastructure.Authentication.SessionValidation.Hash(account.MatKhauHash)),
            new("session_version", sessionVersion.ToString()),
            new(ClaimTypes.Role, account.LoaiTaiKhoan)
        };
        if (account.Email is not null) claims.Add(new(JwtRegisteredClaimNames.Email, account.Email));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(expiresIn),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static CustomerResponse ToCustomerResponse(KhachHang customer) => new(
        $"customers/{customer.Id}", customer.Id, customer.MaKhachHang, customer.HoTen,
        customer.Email, customer.SoDienThoai, customer.NgaySinh, customer.GioiTinh,
        customer.CccdHoChieu, customer.DiaChi, Utc(customer.CreatedAt), Utc(customer.UpdatedAt));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
