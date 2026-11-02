using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed partial class MySqlDatabaseTests
{
    [MySqlFact]
    public async Task Concurrent_bookings_cannot_hold_the_same_room_dates()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_demo.sql")));
        await using var db = database.Context();
        await using var factory = new RealDatabaseApiFactory(db.Database.GetConnectionString()!);
        using var first = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        using var second = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        await LoginAsync(first, "an.nguyen@example.com");
        await LoginAsync(second, "binh.tran@example.com");
        var day = HotelTime.Today.AddDays(60);
        var rooms = await ExpectJsonAsync(first, $"v1/public/rooms/availability?checkInDate={day:yyyy-MM-dd}&checkOutDate={day.AddDays(2):yyyy-MM-dd}&adults=1&children=0");
        var roomId = rooms[0].GetProperty("room").GetProperty("id").GetInt64();
        var request = new { room_ids = new[] { roomId }, check_in = HotelTime.ToUtc(day.ToDateTime(new TimeOnly(14, 0))),
            check_out = HotelTime.ToUtc(day.AddDays(2).ToDateTime(new TimeOnly(12, 0))), adults = 1, children = 0 };
        var responses = await Task.WhenAll(first.PostAsJsonAsync("v1/bookings", request), second.PostAsJsonAsync("v1/bookings", request));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(2, await db.KhoaLichPhongs.CountAsync(x => x.PhongId == roomId && x.NgayLuuTru >= day));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    // Dùng SQL đã đóng gói và seed LMT trên DB riêng, gọi HTTP thật qua test host.
    [MySqlFact]
    public async Task Lmt_demo_seed_is_repeatable_and_app_routes_work_with_real_permissions()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_demo.sql"));
        await database.ExecuteAsync(seed);
        var bookings = await database.ScalarAsync<int>("SELECT COUNT(*) FROM dat_phong");
        var invoices = await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_phong");
        Assert.True(bookings >= 100);
        await database.ExecuteAsync(seed);
        Assert.Equal(bookings, await database.ScalarAsync<int>("SELECT COUNT(*) FROM dat_phong"));
        Assert.Equal(invoices, await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_phong"));
        Assert.Equal(24, await database.ScalarAsync<int>("SELECT COUNT(*) FROM phong"));

        await using var context = database.Context();
        await using var factory = new RealDatabaseApiFactory(context.Database.GetConnectionString()!);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        var appRoutes = new[] { "invoices", "service-invoices", "deposits", "refunds", "shifts", "housekeeping-tasks",
            "room-conditions", "employees", "service-requests", "active-stays", "customers", "reviews", "staff",
            "cancellation-policies", "inventory-items", "assets", "rooms", "deposit-orders" };
        foreach (var route in appRoutes)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("v1/app/" + route)).StatusCode);

        await LoginAsync(client, "admin.com");
        foreach (var route in appRoutes)
        {
            await ExpectJsonAsync(client, "v1/app/" + route);
            await ExpectJsonAsync(client, "v1/app/" + route + "?page=1&pageSize=5&search=DEMO");
        }
        foreach (var route in new[] { "invoices", "purchase-orders", "stock-issues", "inventory-items", "suppliers", "warehouses",
            "services", "housekeeping-tasks", "roles", "permissions", "staff", "room-types" })
            await ExpectJsonAsync(client, "v1/" + route);
        var today = HotelTime.Today;
        foreach (var route in new[] { "inventory", "bookings", "services", "customers", "revenue-breakdown", "operations", "dashboard", "revenue", "occupancy" })
            await ExpectJsonAsync(client, $"v1/reports/{route}?from={today.AddDays(-30):yyyy-MM-dd}&to={today:yyyy-MM-dd}&date={today:yyyy-MM-dd}&warehouseId=1");

        await LoginAsync(client, "an.nguyen@example.com");
        foreach (var route in appRoutes)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("v1/app/" + route)).StatusCode);
        await ExpectJsonAsync(client, "v1/me");
        await ExpectJsonAsync(client, "v1/me/bookings");
        await ExpectJsonAsync(client, "v1/reviews/mine");

        await LoginAsync(client, "kho@hotel.com");
        await ExpectJsonAsync(client, "v1/app/inventory-items");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("v1/app/staff")).StatusCode);
        output.WriteLine($"Seed repeated: {bookings} bookings, {invoices} invoices. Verified 18 app routes, filtering, and guest/customer/warehouse permissions.");
    }

    private static async Task LoginAsync(HttpClient client, string identifier)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("v1/auth/login", new { identifier, password = "12345" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Login {identifier}: {body}");
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("access_token").GetString());
    }

    private static async Task<JsonElement> ExpectJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path}: {(int)response.StatusCode} {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private sealed class RealDatabaseApiFactory(string connectionString, bool fakeGateway = true,
        string environment = "Development", string paymentMode = "Live") : WebApplicationFactory<HotelDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:HotelManagement", connectionString);
            builder.UseSetting("Database:ServerVersion", "11.4.13-mariadb");
            builder.UseSetting("Jwt:SigningKey", ApiFactory.Key);
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");
            builder.UseSetting("BookingMaintenance:Enabled", "false");
            builder.UseSetting("VietQR:Mode", paymentMode);
            // Gateway giả duy nhất ở biên ngân hàng; auth, HTTP và DB đều chạy thật.
            if (fakeGateway) builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVietQrGateway>();
                services.AddSingleton<IVietQrGateway, TestPaymentGateway>();
            });
        }
    }

    private sealed class TestPaymentGateway : IVietQrGateway
    {
        private readonly Dictionary<string, VietQrCreateCustomerQrRequest> orders = new();
        public Task<VietQrCustomerQrResult> CreateCustomerQrAsync(VietQrCreateCustomerQrRequest request, CancellationToken ct)
        {
            orders.Add(request.OrderId, request);
            return Task.FromResult(new VietQrCustomerQrResult("test-qr", "data:image/png;base64,test"));
        }
        public Task<VietQrInquiryResult> InquireAsync(string orderId, CancellationToken ct) =>
            Task.FromResult(new VietQrInquiryResult("00", "test", orderId, orders[orderId].Amount,
                "TEST-" + orderId, "SUCCESS", null, null));
    }
}
