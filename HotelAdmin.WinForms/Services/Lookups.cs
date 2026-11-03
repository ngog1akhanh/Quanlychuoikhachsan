using System.Text.Json;
using HotelAdmin.Models;

namespace HotelAdmin.Services;

/// <summary>
/// Danh mục dùng cho ComboBox (phương thức thanh toán, phòng, nhân viên, kho, mặt hàng...).
/// Tải lại khi mở form để nhận thay đổi từ các trang khác.
/// </summary>
public sealed class Lookups(ApiClient api)
{
    private readonly Dictionary<string, List<JsonElement>> cache = [];

    public void Invalidate(string? key = null)
    {
        if (key is null) cache.Clear();
        else cache.Remove(key);
    }

    /// <summary>Tải danh sách JSON mới nhất từ url.</summary>
    public async Task<List<JsonElement>> ListAsync(string url)
    {
        var rows = J.Items(await api.GetJsonAsync(url));
        cache[url] = rows;
        return rows;
    }

    /// <summary>Danh sách (nhãn, id) từ url với hàm tạo nhãn.</summary>
    public async Task<List<(string Text, long Value)>> OptionsAsync(string url, Func<JsonElement, string> text, Func<JsonElement, bool>? where = null)
        => (await ListAsync(url)).Where(e => where?.Invoke(e) ?? true).Select(e => (text(e), J.Long(e, "id"))).ToList();

    // ---------------- Danh mục hay dùng ----------------
    public const string PaymentMethodsUrl = "v1/payment-methods"; // mọi nhân viên đăng nhập đều đọc được
    public const string RoomBoardUrl = "v1/room-board";
    public const string EmployeesUrl = "v1/app/employees";
    public const string RoomTypesUrl = "v1/room-board/types";
    public const string WarehousesUrl = "v1/warehouses";
    public const string SuppliersUrl = "v1/suppliers?pageSize=100";
    public const string UnitsUrl = "v1/inventory-units";
    public const string GroupsUrl = "v1/inventory-groups";
    public const string ItemsUrl = "v1/inventory-items";
    public const string PositionsUrl = "v1/positions";
    public const string RolesUrl = "v1/roles";
    public const string ServicesUrl = "v1/services/manage?pageSize=100";

    /// <summary>Phương thức thanh toán; cashOnly = chỉ phương thức thu tại quầy (bỏ VietQR).</summary>
    public async Task<List<(string Text, long Value)>> PaymentMethodsAsync(bool cashOnly = true)
        => (await ListAsync(PaymentMethodsUrl))
            .Where(e => !cashOnly || J.Str(e, "ma_phuong_thuc").ToUpperInvariant() is "TIEN_MAT" or "CHUYEN_KHOAN" or "THE" or "CASH" or "BANK_TRANSFER" or "CARD")
            .Select(e => (J.Str(e, "ten_phuong_thuc"), J.Long(e, "id"))).ToList();

    public Task<List<(string Text, long Value)>> RoomsAsync()
        => OptionsAsync(RoomBoardUrl, e => $"{J.Str(e, "room_number")} · {J.Str(e, "room_type_name")}");

    public Task<List<(string Text, long Value)>> EmployeesAsync()
        => OptionsAsync(EmployeesUrl, e => $"{J.Str(e, "full_name")} ({J.Str(e, "position")})");

    public Task<List<(string Text, long Value)>> RoomTypesAsync()
        => OptionsAsync(RoomTypesUrl, e => $"{J.Str(e, "code")} · {J.Str(e, "name")}");

    public Task<List<(string Text, long Value)>> WarehousesAsync(bool activeOnly = true)
        => OptionsAsync(WarehousesUrl, e => J.Str(e, "ten_kho"), e => !activeOnly || J.Str(e, "trang_thai") == "HOAT_DONG");

    public Task<List<(string Text, long Value)>> SuppliersAsync()
        => OptionsAsync(SuppliersUrl, e => J.Str(e, "ten_nha_cung_cap"), e => J.Str(e, "trang_thai") == "HOAT_DONG");

    public Task<List<(string Text, long Value)>> UnitsAsync()
        => OptionsAsync(UnitsUrl, e => J.Str(e, "ten_don_vi"), e => J.Str(e, "trang_thai") == "HOAT_DONG");

    public Task<List<(string Text, long Value)>> GroupsAsync()
        => OptionsAsync(GroupsUrl, e => J.Str(e, "ten_nhom"), e => J.Str(e, "trang_thai") == "HOAT_DONG");

    public Task<List<(string Text, long Value)>> ItemsAsync()
        => OptionsAsync(ItemsUrl, e => $"{J.Str(e, "code")} · {J.Str(e, "name")}");

    public Task<List<(string Text, long Value)>> PositionsAsync()
        => OptionsAsync(PositionsUrl, e => J.Str(e, "name"), e => J.Str(e, "trang_thai") == "HOAT_DONG");

    /// <summary>Tên theo id trong danh sách đã tải (vd. tên kho, nhà cung cấp).</summary>
    public async Task<Dictionary<long, string>> NamesAsync(string url, string namePath)
        => (await ListAsync(url)).GroupBy(e => J.Long(e, "id")).ToDictionary(g => g.Key, g => J.Str(g.First(), namePath));
}
