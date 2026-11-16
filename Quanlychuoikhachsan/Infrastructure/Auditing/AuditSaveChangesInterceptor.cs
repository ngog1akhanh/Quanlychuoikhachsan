using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Infrastructure.Auditing;

/// <summary>
/// Tự ghi nhật ký các thay đổi dữ liệu EF, đồng thời loại trừ trường nhạy cảm.
/// - Sửa / xóa: ghi ngay trước khi lưu (khóa chính đã có).
/// - Thêm mới: ghi SAU khi lưu để có ID thật do database cấp (trước khi lưu EF chỉ có khóa tạm âm → cột ID lộn xộn).
/// - Bỏ các bảng kỹ thuật sinh hàng loạt (khóa lịch từng đêm, giá từng đêm, thông báo, OTP) và lần cập nhật chỉ đổi updated_at.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // Bản ghi vừa thêm, chờ database cấp ID rồi mới ghi nhật ký (interceptor đăng ký Scoped: một DbContext / request).
    private readonly List<EntityEntry> pendingAdded = [];

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

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakeAddedLogs(eventData.Context) is { } context) context.SaveChanges();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (TakeAddedLogs(eventData.Context) is { } context) await context.SaveChangesAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        pendingAdded.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        pendingAdded.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
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
            .Where(entry => !AuditPresenter.SystemTables.Contains(TableName(entry)))
            .ToArray();

        if (entries.Length == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                pendingAdded.Add(entry);
                continue;
            }

            // Lần cập nhật chỉ đổi dấu thời gian kỹ thuật → không có ý nghĩa với người đọc nhật ký.
            if (entry.State == EntityState.Modified && !entry.Properties.Any(p => p.IsModified
                    && !IsSensitive(p.Metadata.Name) && !AuditPresenter.NoiseFields.Contains(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Metadata.Name))))
            {
                continue;
            }

            dbContext.Set<NhatKyHeThong>().Add(CreateLog(entry, entry.State == EntityState.Modified ? "UPDATE" : "DELETE"));
        }
    }

    /// <summary>Sau khi lưu: tạo nhật ký cho các bản ghi vừa thêm (đã có ID thật). Trả về context cần lưu tiếp, hoặc null.</summary>
    private DbContext? TakeAddedLogs(DbContext? dbContext)
    {
        if (dbContext is null || pendingAdded.Count == 0)
        {
            return null;
        }

        var added = pendingAdded.ToArray();
        pendingAdded.Clear();
        foreach (var entry in added)
        {
            if (entry.State == EntityState.Detached) continue;
            dbContext.Set<NhatKyHeThong>().Add(CreateLog(entry, "CREATE"));
        }
        return dbContext;
    }

    private NhatKyHeThong CreateLog(EntityEntry entry, string action)
    {
        var httpContext = httpContextAccessor.HttpContext;
        return new NhatKyHeThong
        {
            TaiKhoanId = GetAccountId(httpContext?.User),
            HanhDong = action,
            DoiTuong = TableName(entry),
            DoiTuongId = GetPrimaryKey(entry),
            DuLieuCu = action is "UPDATE" or "DELETE" ? SerializeValues(entry, useOriginalValues: true) : null,
            DuLieuMoi = action is "CREATE" or "UPDATE" ? SerializeValues(entry, useOriginalValues: false) : null,
            DiaChiIp = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string TableName(EntityEntry entry) => entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;

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
