using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Key = "test-only-signing-key-at-least-32-characters-long";
    private readonly string database = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:HotelManagement", "Server=unused;Database=test;User=test;Password=test");
        builder.UseSetting("Jwt:SigningKey", Key); builder.UseSetting("Jwt:Issuer", "test"); builder.UseSetting("Jwt:Audience", "test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HotelDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<HotelDbContext>>();
            services.RemoveAll<HotelDbContext>();
            services.AddDbContext<HotelDbContext>(o => o.UseInMemoryDatabase(database).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
    public async Task SeedAsync()
    {
        await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
        var store = new TestStore(db); await store.SeedAsync(); await store.SeedStayAsync("DA_TRA");
        var role = new VaiTro { Id = 1, MaVaiTro = "WAREHOUSE", TenVaiTro = "Warehouse", TrangThai = "HOAT_DONG" }; db.Add(role);
        long n = 1;
        foreach (var code in new[] { "INVENTORY_READ", "INVENTORY_WRITE", "PURCHASE_WRITE", "PURCHASE_CONFIRM", "STOCK_ISSUE_WRITE", "STOCK_ISSUE_CONFIRM" })
        { db.Add(new Quyen { Id = n, MaQuyen = code, TenQuyen = code }); db.Add(new VaiTroQuyen { VaiTroId = 1, QuyenId = n++ }); }
        db.Add(new TaiKhoanVaiTro { TaiKhoanId = 1, VaiTroId = 1 }); await db.SaveChangesAsync();
    }
    public static void SignIn(HttpClient client, long id)
    {
        var token = new JwtSecurityToken("test", "test", [new Claim(JwtRegisteredClaimNames.Sub, id.ToString()), new Claim("credential_stamp", SessionValidation.Hash("hash")), new Claim("jti", Guid.NewGuid().ToString())], expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Swagger_resolves_all_routes_without_duplicate_actions()
    {
        await using var f = new ApiFactory(); var client = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/swagger/v1/swagger.json"); var body = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, body);
        using var json = JsonDocument.Parse(body); var paths = json.RootElement.GetProperty("paths");
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "api-openapi.json"), body);
        var access = f.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items.SelectMany(g => g.Items)
            .Select(a => new { path = "/" + a.RelativePath, method = a.HttpMethod,
                anonymous = a.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any(),
                permissions = a.ActionDescriptor.EndpointMetadata.OfType<RequirePermissionAttribute>().Select(p => p.Permission).Distinct().Order().ToArray() });
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "api-access.json"), JsonSerializer.Serialize(access));
        Assert.True(paths.EnumerateObject().Count() > 100); Assert.True(paths.TryGetProperty("/v1/purchase-orders/{id}", out _)); Assert.True(paths.TryGetProperty("/v1/service-requests", out _));
    }
    [Fact]
    public async Task Inventory_requires_permission_and_locked_account_is_rejected()
    {
        await using var f = new ApiFactory(); await f.SeedAsync(); var c = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/v1/purchase-orders")).StatusCode);
        ApiFactory.SignIn(c, 2); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/v1/purchase-orders")).StatusCode);
        ApiFactory.SignIn(c, 1); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/v1/purchase-orders")).StatusCode);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>(); (await db.TaiKhoans.SingleAsync(x => x.Id == 1)).TrangThai = "KHOA"; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/v1/purchase-orders")).StatusCode);
    }
    [Fact]
    public async Task Customer_cannot_read_other_booking_and_invalid_review_rating_is_400()
    {
        await using var f = new ApiFactory(); await f.SeedAsync(); var c = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        ApiFactory.SignIn(c, 3); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/v1/bookings/1")).StatusCode);
        ApiFactory.SignIn(c, 2); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/v1/bookings/1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/v1/reviews", new { booking_id = 1, rating = 6, content = "test" })).StatusCode);
    }
    [Fact]
    public async Task Snake_case_purchase_flow_and_detail_ids_work_over_http()
    {
        await using var f = new ApiFactory(); await f.SeedAsync(); var c = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); ApiFactory.SignIn(c, 1);
        var create = await c.PostAsJsonAsync("/v1/purchase-orders", new { supplier_id = 1, warehouse_id = 1, employee_id = 999, discount = 0, tax = 0, lines = new[] { new { item_id = 3, quantity = 2, unit_price = 5 } } });
        var body = await create.Content.ReadAsStringAsync(); Assert.True(create.StatusCode == HttpStatusCode.Created, body);
        using var json = JsonDocument.Parse(body); var id = json.RootElement.GetProperty("data").GetProperty("id").GetInt64();
        var detail = await c.GetFromJsonAsync<JsonElement>($"/v1/purchase-orders/{id}"); var detailId = detail.GetProperty("chi_tiet")[0].GetProperty("id").GetInt64();
        Assert.Equal(1, detail.GetProperty("nhan_vien_tao_id").GetInt64());
        var confirm = await c.PostAsJsonAsync($"/v1/purchase-orders/{id}:confirm", new { lines = new[] { new { detail_id = detailId } } }); Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/v1/purchase-orders/{id}:confirm", new { lines = new[] { new { detail_id = detailId } } })).StatusCode);
    }
    [Fact]
    public async Task Logout_revokes_the_current_bearer_token()
    {
        await using var f = new ApiFactory(); await f.SeedAsync(); var c = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); ApiFactory.SignIn(c, 2);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/v1/me")).StatusCode);
    }
}
