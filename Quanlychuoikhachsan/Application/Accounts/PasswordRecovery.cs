using System.Data;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Accounts;

public interface IRecoveryEmailSender { Task SendAsync(string email, string token, CancellationToken ct); }
public sealed class SmtpRecoveryEmailSender(IConfiguration config) : IRecoveryEmailSender
{
    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        var host = config["Smtp:Host"]; var from = config["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from)) throw new ServiceUnavailableException("Password recovery email is not configured.");
        using var smtp = new SmtpClient(host, config.GetValue("Smtp:Port", 587)) { EnableSsl = true, Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]) };
        using var message = new MailMessage(from, email, "Đặt lại mật khẩu khách sạn", $"Mã đặt lại mật khẩu của bạn: {token}\nMã có hiệu lực 15 phút và chỉ dùng một lần.");
        await smtp.SendMailAsync(message, ct);
    }
}
public sealed class PasswordRecovery(HotelDbContext db, IPasswordHasher<TaiKhoan> hasher, IRecoveryEmailSender sender, IConfiguration config)
{
    public async Task RequestAsync(string email, CancellationToken ct)
    {
        // Check configuration before account lookup to keep unknown/known responses identical.
        if (string.IsNullOrWhiteSpace(config["Smtp:Host"]) || string.IsNullOrWhiteSpace(config["Smtp:From"])) throw new ServiceUnavailableException("Password recovery email is not configured.");
        var account = await db.TaiKhoans.SingleOrDefaultAsync(x => x.Email == email.Trim().ToLower() && x.TrangThai == "HOAT_DONG", ct);
        if (account is null) return;
        if (await db.Set<MaXacThuc>().AnyAsync(x => x.TaiKhoanId == account.Id && x.Loai == "QUEN_MAT_KHAU" && x.CreatedAt > DateTime.UtcNow.AddMinutes(-1), ct)) return;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Add(new MaXacThuc { TaiKhoanId = account.Id, Loai = "QUEN_MAT_KHAU", MaHash = SessionValidation.Hash(token), HetHanLuc = DateTime.UtcNow.AddMinutes(15), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await sender.SendAsync(account.Email!, token, ct);
    }
    public async Task ResetAsync(string token, string password, CancellationToken ct)
    {
        var hash = SessionValidation.Hash(token);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.Set<MaXacThuc>().FromSqlInterpolated($"SELECT * FROM ma_xac_thuc WHERE ma_hash = {hash} AND loai = 'QUEN_MAT_KHAU' FOR UPDATE").ToListAsync(ct);
        var code = await db.Set<MaXacThuc>().SingleOrDefaultAsync(x => x.MaHash == hash && x.Loai == "QUEN_MAT_KHAU" && !x.DaSuDung && x.HetHanLuc > DateTime.UtcNow, ct) ?? throw new InvalidArgumentException("Reset token is invalid or expired.");
        var account = await db.TaiKhoans.SingleAsync(x => x.Id == code.TaiKhoanId, ct);
        if (account.TrangThai != "HOAT_DONG") throw new InvalidArgumentException("Reset token is invalid or expired.");
        account.MatKhauHash = hasher.HashPassword(account, password); account.UpdatedAt = DateTime.UtcNow;
        var codes = await db.Set<MaXacThuc>().Where(x => x.TaiKhoanId == account.Id && x.Loai == "QUEN_MAT_KHAU" && !x.DaSuDung).ToListAsync(ct);
        foreach (var c in codes) { c.DaSuDung = true; c.SuDungLuc = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
