using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quanlychuoikhachsan.Api.Controllers;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Tests.Payments;

public sealed class PaymentAuthorizationTests
{
    [Fact]
    public async Task Manager_can_read_and_edit_notes_but_cannot_change_payment_status()
    {
        await using var db = CreateDbContext();
        await SeedPermissionsAsync(db);
        var handler = new PermissionAuthorizationHandler(db);

        Assert.True(await HasPermissionAsync(handler, ManagerAccountId, "PAYOS_PAYMENT_READ"));
        Assert.True(await HasPermissionAsync(handler, ManagerAccountId, "PAYOS_PAYMENT_NOTE_WRITE"));
        Assert.False(await HasPermissionAsync(handler, ManagerAccountId, "PAYOS_PAYMENT_STATUS_WRITE"));
    }

    [Fact]
    public async Task Admin_has_payment_status_permission()
    {
        await using var db = CreateDbContext();
        await SeedPermissionsAsync(db);
        var handler = new PermissionAuthorizationHandler(db);

        Assert.True(await HasPermissionAsync(handler, AdminAccountId, "PAYOS_PAYMENT_STATUS_WRITE"));
    }

    [Fact]
    public void Payment_management_actions_require_the_intended_permissions()
    {
        AssertPermission(nameof(PayOsPaymentManagementController.List), "PAYOS_PAYMENT_READ");
        AssertPermission(nameof(PayOsPaymentManagementController.UpdateNote), "PAYOS_PAYMENT_NOTE_WRITE");
        AssertPermission(nameof(PayOsPaymentManagementController.UpdateStatus), "PAYOS_PAYMENT_STATUS_WRITE");
    }

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(PayOsPaymentManagementController).GetMethod(methodName);
        Assert.NotNull(method);
        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(expected, attribute?.Permission);
    }

    private static async Task<bool> HasPermissionAsync(
        PermissionAuthorizationHandler handler,
        long accountId,
        string permission)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test"));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static HotelDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HotelDbContext(options);
    }

    private static async Task SeedPermissionsAsync(HotelDbContext db)
    {
        var admin = new VaiTro { Id = 1, MaVaiTro = "ADMIN", TenVaiTro = "Admin" };
        var manager = new VaiTro { Id = 2, MaVaiTro = "MANAGER", TenVaiTro = "Manager" };
        var permissions = new[]
        {
            new Quyen { Id = 1, MaQuyen = "PAYOS_PAYMENT_READ", TenQuyen = "Read" },
            new Quyen { Id = 2, MaQuyen = "PAYOS_PAYMENT_NOTE_WRITE", TenQuyen = "Note" },
            new Quyen { Id = 3, MaQuyen = "PAYOS_PAYMENT_STATUS_WRITE", TenQuyen = "Status" }
        };
        db.VaiTros.AddRange(admin, manager);
        db.Quyens.AddRange(permissions);
        db.TaiKhoans.AddRange(
            new TaiKhoan { Id = AdminAccountId, MatKhauHash = "hash", LoaiTaiKhoan = "NHAN_VIEN" },
            new TaiKhoan { Id = ManagerAccountId, MatKhauHash = "hash", LoaiTaiKhoan = "NHAN_VIEN" });
        db.TaiKhoanVaiTros.AddRange(
            new TaiKhoanVaiTro { TaiKhoanId = AdminAccountId, VaiTroId = admin.Id },
            new TaiKhoanVaiTro { TaiKhoanId = ManagerAccountId, VaiTroId = manager.Id });
        db.VaiTroQuyens.AddRange(
            new VaiTroQuyen { VaiTroId = admin.Id, QuyenId = permissions[0].Id },
            new VaiTroQuyen { VaiTroId = admin.Id, QuyenId = permissions[1].Id },
            new VaiTroQuyen { VaiTroId = admin.Id, QuyenId = permissions[2].Id },
            new VaiTroQuyen { VaiTroId = manager.Id, QuyenId = permissions[0].Id },
            new VaiTroQuyen { VaiTroId = manager.Id, QuyenId = permissions[1].Id });
        await db.SaveChangesAsync();
    }

    private const long AdminAccountId = 101;
    private const long ManagerAccountId = 102;
}
