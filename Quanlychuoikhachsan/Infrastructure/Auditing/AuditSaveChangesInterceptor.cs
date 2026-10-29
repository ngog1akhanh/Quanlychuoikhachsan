using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Infrastructure.Auditing;

/// <summary>Tự ghi nhật ký các thay đổi dữ liệu EF, đồng thời loại trừ trường nhạy cảm.</summary>
public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        // Bỏ qua chính bảng nhật ký để tránh tạo vòng lặp khi SaveChanges.
        var entries = dbContext.ChangeTracker.Entries()
            .Where(entry => entry.Entity is not NhatKyHeThong && entry.State is
                EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (entries.Length == 0)
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var accountId = GetAccountId(httpContext?.User);

        foreach (var entry in entries)
        {
            var tableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            var action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => "UNKNOWN"
            };

            var oldValues = entry.State is EntityState.Modified or EntityState.Deleted
                ? SerializeValues(entry, useOriginalValues: true)
                : null;
            var newValues = entry.State is EntityState.Added or EntityState.Modified
                ? SerializeValues(entry, useOriginalValues: false)
                : null;

            dbContext.Set<NhatKyHeThong>().Add(new NhatKyHeThong
            {
                TaiKhoanId = accountId,
                HanhDong = action,
                DoiTuong = tableName,
                DoiTuongId = GetPrimaryKey(entry),
                DuLieuCu = oldValues,
                DuLieuMoi = newValues,
                DiaChiIp = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static string? SerializeValues(EntityEntry entry, bool useOriginalValues)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            if (IsSensitive(property.Metadata.Name) ||
                (entry.State == EntityState.Modified && !property.IsModified))
            {
                continue;
            }

            values[property.Metadata.Name] = useOriginalValues
                ? property.OriginalValue
                : property.CurrentValue;
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static bool IsSensitive(string propertyName) =>
        propertyName.Contains("MatKhau", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("MaHash", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("mat_khau", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("token", StringComparison.OrdinalIgnoreCase);

    private static string? GetPrimaryKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        return string.Join(",", key.Properties.Select(property =>
            Convert.ToString(entry.Property(property.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static long? GetAccountId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");
        return long.TryParse(value, out var accountId) ? accountId : null;
    }
}
