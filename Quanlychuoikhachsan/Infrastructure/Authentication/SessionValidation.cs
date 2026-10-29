using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Infrastructure.Authentication;

public static class SessionValidation
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<HotelDbContext>();
        var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirstValue("sub");
        if (!long.TryParse(idText, out var id)) { context.Fail("Invalid account."); return; }
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
        var stamp = context.Principal?.FindFirstValue("credential_stamp");
        var jti = context.Principal?.FindFirstValue("jti");
        if (account is null || account.TrangThai != "HOAT_DONG" || stamp != Hash(account.MatKhauHash) || string.IsNullOrEmpty(jti))
        { context.Fail("Account or session is no longer active."); return; }
        var hash = Hash(jti);
        var version = await db.Set<MaXacThuc>().Where(x => x.TaiKhoanId == id && x.Loai == "SESSION_VERSION").Select(x => (long?)x.Id).MaxAsync(context.HttpContext.RequestAborted) ?? 0;
        var tokenVersion = context.Principal?.FindFirstValue("session_version") ?? "0";
        if (tokenVersion != version.ToString()) { context.Fail("Session has been revoked."); return; }
        if (await db.Set<MaXacThuc>().AnyAsync(x => x.TaiKhoanId == id && x.Loai == "JWT_REVOKED" && x.MaHash == hash && x.HetHanLuc > DateTime.UtcNow, context.HttpContext.RequestAborted)) context.Fail("Session has been logged out.");
    }
}
