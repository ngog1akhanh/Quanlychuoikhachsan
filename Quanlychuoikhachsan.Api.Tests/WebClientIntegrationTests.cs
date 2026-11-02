using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using QuanLyKhachSan.Web.Services;
using QuanLyKhachSan.Web.State;
using Quanlychuoikhachsan.Application.Common;
using Xunit;
using WebBooking = QuanLyKhachSan.Web.Services.BookingService;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed partial class MySqlDatabaseTests
{
    [MySqlFact]
    public async Task Website_client_can_login_search_book_pay_deposit_and_request_cancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(TestDatabase.Root, "Quanlychuoikhachsan/Database/Scripts/seed_demo.sql")));
        await using var db = database.Context();
        await using var factory = new RealDatabaseApiFactory(db.Database.GetConnectionString()!);
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        var state = new AuthState(new ProtectedSessionStorage(new NoBrowserStorage(), new EphemeralDataProtectionProvider()));
        var api = new ApiClient(http, state, Options.Create(new ApiOptions { UseMockData = false }));
        var auth = new AuthService(api, state);
        await auth.LoginAsync(new() { Identifier = "an.nguyen@example.com", Password = "12345" });
        Assert.True(state.IsAuthenticated);
        Assert.True((await new CustomerService(api).GetProfileAsync()).Id > 0);
        var highlights = await new RoomService(api).GetHighlightsAsync();
        Assert.Equal(4, highlights.Count);
        Assert.Equal(800000m, highlights.Single(x => x.RoomTypeCode == "STD").BaseNightlyPrice);
        Assert.Contains(highlights, x => x.RoomTypeCode == "SUITE");

        var search = new QuanLyKhachSan.Web.Models.Room.RoomSearchQuery
        { CheckIn = HotelTime.Today.AddDays(50), CheckOut = HotelTime.Today.AddDays(52), Adults = 1 };
        var rooms = await new RoomService(api).SearchAvailableAsync(search);
        Assert.NotEmpty(rooms);
        var bookings = new WebBooking(api);
        var booking = await bookings.CreateAsync(new([rooms[0].Id],
            HotelTime.ToUtc(search.CheckIn.ToDateTime(new TimeOnly(14, 0))),
            HotelTime.ToUtc(search.CheckOut.ToDateTime(new TimeOnly(12, 0))), 1, 0, "Web integration"));
        Assert.True(booking.Id > 0);
        Assert.Equal("HOLD", booking.Status);
        Assert.NotNull(await bookings.GetMyBookingAsync(booking.Id));
        await bookings.UpdateSpecialRequestAsync(booking.Id, "Updated from website");

        var payments = new PaymentService(api);
        var order = await payments.CreateDepositOrderAsync(booking.Id);
        Assert.Equal("PENDING", order.Status);
        Assert.True(order.Amount > 0);
        Assert.Equal("PAID", (await payments.InquireDepositAsync(order.OrderId)).Status);
        Assert.Equal("PAID", (await payments.InquireDepositAsync(order.OrderId)).Status);
        Assert.Equal(1, await db.PhieuCocs.CountAsync(x => x.DatPhongId == booking.Id));
        Assert.Equal("BOOKED", (await bookings.GetMyBookingAsync(booking.Id))!.Status);
        var cancellation = await bookings.RequestCancelAsync(booking.Id, "Test request");
        Assert.True(cancellation.Id > 0);
        Assert.Equal("CHO_DUYET", cancellation.Status);
        Assert.Equal(booking.Id, cancellation.BookingId);
    }

    // Chỉ thay bộ nhớ trình duyệt; client vẫn dùng đúng DTO, JSON và request của web.
    private sealed class NoBrowserStorage : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
