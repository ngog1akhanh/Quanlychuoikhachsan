using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Common;

public static class Input
{
    public static string Text(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new InvalidArgumentException($"{field} is required and must not exceed {max} characters.");
        return value.Trim();
    }
    public static void Optional(string? value, int max, string field)
    {
        if (value?.Length > max) throw new InvalidArgumentException($"{field} must not exceed {max} characters.");
    }
    public static void Money(decimal value, string field)
    {
        if (value < 0 || value > 9999999999999.99m || decimal.Round(value, 2) != value)
            throw new InvalidArgumentException($"{field} must be nonnegative with at most two decimal places.");
    }
    public static void Quantity(decimal value)
    {
        if (value <= 0 || value > 999999999999.999m || decimal.Round(value, 3) != value)
            throw new InvalidArgumentException("Quantity must be positive with at most three decimal places.");
    }
    public static void OneOf(string? value, params string[] choices)
    {
        if (value is null || !choices.Contains(value)) throw new InvalidArgumentException($"Expected one of: {string.Join(", ", choices)}.");
    }
    public static void Range(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366 || to == DateOnly.MaxValue)
            throw new InvalidArgumentException("Date range must be ordered and no longer than 366 days.");
    }
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class CurrentActor(HotelDbContext db, IHttpContextAccessor accessor)
{
    public long AccountId => long.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : throw new UnauthenticatedException();

    public async Task<long> EmployeeIdAsync(CancellationToken ct)
        => await db.NhanViens.Where(x => x.TaiKhoanId == AccountId && x.TrangThai == "DANG_LAM"
            && x.TaiKhoan.TrangThai == "HOAT_DONG").Select(x => (long?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new PermissionDeniedException("An active employee profile is required.");

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken ct)
        => await db.TaiKhoanVaiTros.AnyAsync(x => x.TaiKhoanId == AccountId
            && x.TaiKhoan.TrangThai == "HOAT_DONG" && x.VaiTro.TrangThai == "HOAT_DONG"
            && x.VaiTro.VaiTroQuyens.Any(p => p.Quyen.MaQuyen == permission), ct);

    public async Task<DatPhong> BookingAsync(long id, string staffPermission, CancellationToken ct)
    {
        var booking = await db.DatPhongs.Include(x => x.KhachHang).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ResourceNotFoundException("Booking was not found.");
        if (booking.KhachHang.TaiKhoanId != AccountId && !await HasPermissionAsync(staffPermission, ct))
            throw new ResourceNotFoundException("Booking was not found.");
        return booking;
    }
}

public sealed record ListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null)
{
    public int Offset => (Math.Clamp(Page, 1, 100000) - 1) * Size;
    public int Size => Math.Clamp(PageSize, 1, 100);
}
public sealed record ResultPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public static class QueryPaging
{
    public static async Task<ResultPage<T>> PageAsync<T>(this IQueryable<T> query, ListQuery page, CancellationToken ct)
        => new(await query.Skip(page.Offset).Take(page.Size).ToListAsync(ct), await query.CountAsync(ct), Math.Clamp(page.Page, 1, 100000), page.Size);
}
public sealed record StatusRequest([param: Required, StringLength(30)] string Status);
public sealed record ReasonRequest([param: Required, StringLength(500)] string Reason);
