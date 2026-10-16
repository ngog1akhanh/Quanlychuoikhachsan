using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using System.Text.Json;

namespace Quanlychuoikhachsan.Infrastructure.Authentication;

/// <summary>Tạo policy động theo mã quyền được khai báo trên endpoint.</summary>
public static class PermissionPolicies
{
    public const string Prefix = "permission:";
    public static string For(string permission) => $"{Prefix}{permission}";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PermissionPolicies.For(permission);
    }

    public string Permission { get; }
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallbackProvider.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallbackProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PermissionPolicies.Prefix.Length..];
            var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallbackProvider.GetPolicyAsync(policyName);
    }
}

/// <summary>Đối chiếu quyền của tài khoản với vai trò/quyền đang lưu trong database.</summary>
public sealed class PermissionAuthorizationHandler(HotelDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!long.TryParse(subject, out var accountId))
        {
            return;
        }

        var hasPermission = await dbContext.TaiKhoanVaiTros
            .Where(accountRole => accountRole.TaiKhoanId == accountId)
            .Join(dbContext.VaiTroQuyens,
                accountRole => accountRole.VaiTroId,
                rolePermission => rolePermission.VaiTroId,
                (_, rolePermission) => rolePermission.QuyenId)
            .Join(dbContext.Quyens,
                permissionId => permissionId,
                permission => permission.Id,
                (_, permission) => permission.MaQuyen)
            .AnyAsync(permissionCode => permissionCode == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Định dạng phản hồi lỗi 401/403 theo hợp đồng API tương ứng.</summary>
public static class AuthenticationErrorResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task WriteAsync(HttpResponse response, int code, string status, string message)
    {
        response.StatusCode = code;
        response.ContentType = "application/json; charset=utf-8";
        var path = response.HttpContext.Request.Path;
        if (path.StartsWithSegments("/v1/payments/vietqr") ||
            path.Value?.Contains("/vietqr-payment", StringComparison.OrdinalIgnoreCase) == true)
        {
            await JsonSerializer.SerializeAsync(
                response.Body,
                new PaymentApiResponse<object?>(false, message, null, response.HttpContext.TraceIdentifier),
                JsonOptions);
            return;
        }

        var payload = new GoogleErrorResponse(new GoogleError(code, message, status, []));
        await JsonSerializer.SerializeAsync(response.Body, payload, JsonOptions);
    }
}
