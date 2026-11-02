using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed partial class MySqlDatabaseTests
{
    [MySqlFact]
    public async Task Historical_seed_is_repeatable_and_invoice_reports_reconcile()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scripts = Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts");
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(scripts, "seed_demo.sql")));
        var history = await File.ReadAllTextAsync(Path.Combine(scripts, "seed_thong_ke.sql"));
        await database.ExecuteAsync(history);
        var count = await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_phong WHERE ma_hoa_don LIKE 'HD-TK-%'");
        Assert.True(count > 250);
        var serviceCount = await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_dich_vu WHERE ma_hoa_don LIKE 'HDV-TK-%'");
        Assert.True(serviceCount > 100);
        var details = await database.ScalarAsync<int>("SELECT COUNT(*) FROM chi_tiet_dat_phong");
        await database.ExecuteAsync(history);
        Assert.Equal(count, await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_phong WHERE ma_hoa_don LIKE 'HD-TK-%'"));
        Assert.Equal(serviceCount, await database.ScalarAsync<int>("SELECT COUNT(*) FROM hoa_don_dich_vu WHERE ma_hoa_don LIKE 'HDV-TK-%'"));
        Assert.Equal(details, await database.ScalarAsync<int>("SELECT COUNT(*) FROM chi_tiet_dat_phong"));

        await using var db = database.Context();
        await using var factory = new RealDatabaseApiFactory(db.Database.GetConnectionString()!);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        await LoginAsync(client, "admin.com");
        const string range = "?fromYear=2024&toYear=2026";
        var periods = await ExpectJsonAsync(client, "v1/reports/invoice-stats/periods" + range + "&groupBy=month");
        var staff = await ExpectJsonAsync(client, "v1/reports/invoice-stats/staff" + range + "&groupBy=quarter");
        var products = await ExpectJsonAsync(client, "v1/reports/invoice-stats/products" + range);
        Assert.Equal(36, periods.GetProperty("periods").GetArrayLength());
        var total = periods.GetProperty("total").GetDecimal();
        Assert.True(total > 0);
        Assert.Equal(total, staff.GetProperty("staff").EnumerateArray().Sum(x => x.GetProperty("total").GetDecimal()));
        Assert.True(products.GetProperty("service_total").GetDecimal() > 0);
        Assert.True(products.GetProperty("room_total").GetDecimal() > 0);
        Assert.Equal(periods.GetProperty("service_total").GetDecimal(), products.GetProperty("service_total").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("v1/reports/invoice-stats/periods?fromYear=2026&toYear=2024")).StatusCode);
        await LoginAsync(client, "an.nguyen@example.com");
        foreach (var report in new[] { "periods", "staff", "products" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("v1/reports/invoice-stats/" + report + range)).StatusCode);
    }

    [MySqlFact]
    public async Task Demo_deposit_runs_real_gateway_once_and_rejects_other_users_live_and_production()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_demo.sql")));
        await using var db = database.Context();
        var connection = db.Database.GetConnectionString()!;
        await using var factory = new RealDatabaseApiFactory(connection, fakeGateway: false, paymentMode: "Demo");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        await LoginAsync(client, "an.nguyen@example.com");
        var day = HotelTime.Today.AddDays(70);
        var rooms = await ExpectJsonAsync(client, $"v1/public/rooms/availability?checkInDate={day:yyyy-MM-dd}&checkOutDate={day.AddDays(1):yyyy-MM-dd}&adults=1&children=0");
        using var created = await client.PostAsJsonAsync("v1/bookings", new
        {
            room_ids = new[] { rooms[0].GetProperty("room").GetProperty("id").GetInt64() },
            check_in = HotelTime.ToUtc(day.ToDateTime(new TimeOnly(14, 0))),
            check_out = HotelTime.ToUtc(day.AddDays(1).ToDateTime(new TimeOnly(12, 0))), adults = 1, children = 0
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync());
        var bookingId = booking.GetProperty("data").GetProperty("id").GetInt64();
        using var qrResponse = await client.PostAsJsonAsync($"v1/bookings/{bookingId}/deposit-payment", new { });
        qrResponse.EnsureSuccessStatusCode();
        var qr = JsonSerializer.Deserialize<JsonElement>(await qrResponse.Content.ReadAsStringAsync());
        Assert.True(qr.GetProperty("demo").GetBoolean());
        Assert.StartsWith("000201", qr.GetProperty("qr_code").GetString());
        Assert.True(qr.GetProperty("hold_expires_at").GetDateTime() > DateTime.UtcNow);
        var orderId = qr.GetProperty("order_id").GetString();
        var simulate = $"v1/deposit-payments/{orderId}:simulate-paid";
        using var inquiry = await client.PostAsJsonAsync($"v1/deposit-payments/{orderId}/inquiry", new { });
        inquiry.EnsureSuccessStatusCode();
        Assert.Equal("PENDING", JsonSerializer.Deserialize<JsonElement>(await inquiry.Content.ReadAsStringAsync()).GetProperty("status").GetString());
        await LoginAsync(client, "binh.tran@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(simulate, new { })).StatusCode);

        foreach (var (environment, mode, expected) in new[] {
            ("Production", "Demo", HttpStatusCode.NotFound), ("Development", "Live", HttpStatusCode.BadRequest) })
        {
            await using var restricted = new RealDatabaseApiFactory(connection, fakeGateway: false, environment: environment, paymentMode: mode);
            using var restrictedClient = restricted.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
            await LoginAsync(restrictedClient, "an.nguyen@example.com");
            Assert.Equal(expected, (await restrictedClient.PostAsJsonAsync(simulate, new { })).StatusCode);
        }
        await LoginAsync(client, "an.nguyen@example.com");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var paid = await client.PostAsJsonAsync(simulate, new { });
            paid.EnsureSuccessStatusCode();
            Assert.Equal("PAID", JsonSerializer.Deserialize<JsonElement>(await paid.Content.ReadAsStringAsync()).GetProperty("status").GetString());
        }
        Assert.Equal(1, await db.PhieuCocs.CountAsync(x => x.DatPhongId == bookingId));
        Assert.Equal("BOOKED", (await db.DatPhongs.SingleAsync(x => x.Id == bookingId)).TrangThai);
    }
}
