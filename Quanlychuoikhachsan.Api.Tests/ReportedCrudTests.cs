using Microsoft.EntityFrameworkCore;
using HotelAdmin.Models;
using HotelAdmin.Services;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed partial class MySqlDatabaseTests
{
    [MySqlFact]
    public async Task App_can_create_housekeeping_edit_receive_issue_stock_and_update_staff()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_demo.sql")));
        var operations = await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_operations_demo.sql"));
        await database.ExecuteAsync(operations);
        await database.ExecuteAsync(operations);
        Assert.Equal(3, await database.ScalarAsync<int>("SELECT COUNT(*) FROM cong_viec_buong_phong WHERE ma_cong_viec LIKE 'DEMO-OPS-HK-%'"));
        Assert.Equal(10m, await database.ScalarAsync<decimal>("SELECT SUM(so_luong_thay_doi) FROM giao_dich_kho WHERE ma_giao_dich='DEMO-OPS-NK'"));
        await using var db = database.Context();
        await using var factory = new RealDatabaseApiFactory(db.Database.GetConnectionString()!);
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        await LoginAsync(http, "admin.com");
        var api = new ApiClient(http, http.BaseAddress!);
        var lookups = new Lookups(api);
        var room = (await lookups.RoomsAsync()).First();
        var task = await api.PostJsonAsync("v1/housekeeping-tasks", new { room_id = room.Value, type = "KIEM_TRA", priority = "BINH_THUONG" });
        var taskId = J.Long(task, "id");
        Assert.True(taskId > 0);
        await api.PatchJsonAsync($"v1/housekeeping-tasks/{taskId}/status", new { status = "DANG_THUC_HIEN" });
        await api.PatchJsonAsync($"v1/housekeeping-tasks/{taskId}/status", new { status = "HOAN_THANH" });
        var unit = (await lookups.UnitsAsync()).First().Value;
        var item = await api.PostJsonAsync("v1/inventory-items", new { code = "BUG-ITEM", name = "Hàng kiểm thử", type = "VAT_TU", unit_id = unit, reference_cost = 100, minimum_stock = 0, track_lot = false, track_expiry = false, track_individual = false });
        var itemId = J.Long(item, "id");
        Assert.True(itemId > 0);
        var supplier = (await lookups.SuppliersAsync()).First().Value;
        var warehouse = (await lookups.WarehousesAsync()).First().Value;
        var purchase = await api.PostJsonAsync("v1/purchase-orders", new { supplier_id = supplier, warehouse_id = warehouse, lines = new[] { new { item_id = itemId, quantity = 2, unit_price = 100 } } });
        var purchaseId = J.Long(purchase, "id");
        Assert.True(purchaseId > 0);
        await api.PutJsonAsync($"v1/purchase-orders/{purchaseId}", new { supplier_id = supplier, warehouse_id = warehouse, lines = new[] { new { item_id = itemId, quantity = 3, unit_price = 120 } }, note = "Updated draft" });
        var detail = await api.GetJsonAsync($"v1/purchase-orders/{purchaseId}");
        Assert.Equal(360m, J.Dec(detail, "tong_thanh_toan"));
        var line = Assert.Single(J.Items(detail, "chi_tiet"));
        Assert.Equal(3m, J.Dec(line, "so_luong"));
        await api.PostJsonAsync($"v1/purchase-orders/{purchaseId}:confirm", new { lines = new[] { new { detail_id = J.Long(line, "id") } } });
        var balance = await api.GetJsonAsync($"v1/warehouses/{warehouse}/inventory-items/{itemId}/balance");
        Assert.Equal(3m, J.Dec(balance, "balance"));
        var issue = await api.PostJsonAsync("v1/stock-issues", new { warehouse_id = warehouse, purpose = "KHAC", recipient = "QA", reason = "Test", lines = new[] { new { item_id = itemId, quantity = 1 } } });
        await api.PostJsonAsync($"v1/stock-issues/{J.Long(issue, "id")}:confirm");
        Assert.Equal(2m, J.Dec(await api.GetJsonAsync($"v1/warehouses/{warehouse}/inventory-items/{itemId}/balance"), "balance"));
        var position = (await lookups.PositionsAsync()).First().Value;
        var staff = await api.PostJsonAsync("v1/staff", new { code = "BUG-NV", full_name = "Test staff", position_id = position, email = "bug-staff@example.com", phone = "0999999999", password = "test-pass-123" });
        var staffId = J.Long(staff, "id");
        Assert.True(staffId > 0);
        await api.PutJsonAsync($"v1/staff/{staffId}", new { full_name = "Updated staff", position_id = position, status = "DANG_LAM", address = "QA" });
        var updated = Assert.Single(J.Items(await api.GetJsonAsync("v1/app/staff?search=BUG-NV")));
        Assert.Equal("Updated staff", J.Str(updated, "full_name"));
        var role = Assert.Single(J.Items(await api.GetJsonAsync("v1/roles")), x => J.Str(x, "role_code") == "WAREHOUSE");
        await api.PutJsonAsync($"v1/accounts/{J.Long(staff, "account_id")}/roles", new { ids = new[] { J.Long(role, "id") } });
        var assigned = Assert.Single(J.Items(await api.GetJsonAsync("v1/app/staff?search=BUG-NV")));
        Assert.Contains(J.Items(assigned, "role_ids"), x => x.GetInt64() == J.Long(role, "id"));
        var me = await api.GetJsonAsync("v1/me");
        await api.PostJsonAsync("v1/notifications", new { title = "QA hộp thư", content = "Thông báo kiểm thử", account_ids = new[] { J.Long(me, "account_id") } });
        var notification = Assert.Single(J.Items(await api.GetJsonAsync("v1/notifications?pageSize=100")), x => J.Str(x, "notification.tieu_de") == "QA hộp thư");
        var notificationId = J.Long(notification, "notification.id");
        await api.PostJsonAsync($"v1/notifications/{notificationId}:read");
        Assert.DoesNotContain(J.Items(await api.GetJsonAsync("v1/notifications?unreadOnly=true&pageSize=100")), x => J.Long(x, "notification.id") == notificationId);
    }
}
