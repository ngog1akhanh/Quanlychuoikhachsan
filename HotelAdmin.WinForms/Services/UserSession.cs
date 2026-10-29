using HotelAdmin.Models;

namespace HotelAdmin.Services;

/// <summary>Thông tin người dùng đang đăng nhập (lấy từ /v1/me).</summary>
public sealed class UserSession(MeResponse me, DateTime expiresAt)
{
    public MeResponse Me { get; } = me;
    public DateTime ExpiresAt { get; } = expiresAt;

    public string DisplayName => Me.DisplayName;

    /// <summary>Tên vai trò hiển thị dưới avatar (ví dụ "Quản trị hệ thống").</summary>
    public string RoleTitle => Me.Roles.Count == 0 ? "Chưa gán vai trò" : string.Join(", ", Me.Roles.Select(r => r.Name));

    public bool IsAdmin => HasRole(Roles.Admin);

    // Dữ liệu LMT cũ và seed API mới dùng hai bộ mã cho cùng vai trò.
    public bool HasRole(string code) => Me.Roles.Any(r => Roles.Canonical(r.Code) == Roles.Canonical(code));

    public bool HasAnyRole(IEnumerable<string> codes) => IsAdmin || codes.Any(HasRole);

    public bool HasPermission(string code) => Me.Permissions.Contains(code, StringComparer.OrdinalIgnoreCase);

    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            return parts.Length == 1
                ? parts[0][..1].ToUpperInvariant()
                : (parts[^2][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }
}

/// <summary>Mã vai trò dùng trong bảng vai_tro.</summary>
public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Receptionist = "RECEPTIONIST";
    public const string Housekeeping = "HOUSEKEEPING";
    public const string Service = "SERVICE";
    public const string Warehouse = "WAREHOUSE";

    public static string Canonical(string code) => code.ToUpperInvariant() switch
    {
        "LE_TAN" => Receptionist,
        "BUONG_PHONG" => Housekeeping,
        "DICH_VU" => Service,
        "KHO" => Warehouse,
        var value => value
    };
}
