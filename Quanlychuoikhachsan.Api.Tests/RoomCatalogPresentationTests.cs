using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyKhachSan.Web.Components.Rooms;
using QuanLyKhachSan.Web.Models.Room;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class RoomCatalogPresentationTests
{
    [Theory]
    [InlineData("Phòng 101", null)]
    [InlineData("Hai giường Queen, rộng rãi", "2 giường queen")]
    [InlineData("2 giường đơn", "2 giường đơn")]
    [InlineData("1 giường đôi và 1 giường đơn", "1 giường đôi + 1 giường đơn")]
    public void Beds_require_explicit_data(string description, string? expected)
        => Assert.Equal(expected, BedConfiguration.FromDescription(description));

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(8, false)]
    public async Task Card_shows_scarcity_only_below_five_and_hides_physical_room_details(int available, bool showScarcity)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var room = new PublicRoom(1, "SECRET101", "STD", "Standard", 800000, 0, "SECRET_FLOOR",
            "SECRET_VIEW", 28, "Phòng SECRET101", 2, 1, BedDescription: "2 giường đơn");
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<RoomCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Room"] = room, ["AvailableCount"] = available }));
            return System.Net.WebUtility.HtmlDecode(result.ToHtmlString());
        });
        Assert.Equal(showScarcity, html.Contains("Chỉ còn"));
        Assert.Contains("Standard", html);
        Assert.Contains("2 giường đơn", html);
        Assert.DoesNotContain("SECRET", html);
    }

    [Fact]
    public async Task Room_list_groups_by_class_and_beds_not_room_number_or_view()
    {
        var state = new QuanLyKhachSan.Web.State.BookingState();
        var twin = new PublicRoom(1, "SECRET101", "STD", "Standard", 800000, 0, "1", "River", 28,
            null, 2, 0, BedDescription: "2 giường đơn");
        state.SetResults(new RoomSearchQuery(), new[] { twin, twin with { Id = 2, RoomNumber = "SECRET102", ViewType = "City" },
            twin with { Id = 3, BedDescription = "1 giường đôi" } });
        using var services = new ServiceCollection().AddLogging()
            .AddSingleton(state)
            .AddSingleton(new QuanLyKhachSan.Web.Services.RoomService(null!))
            .AddSingleton<NavigationManager>(new TestNavigation()).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => {
            var result = await renderer.RenderComponentAsync<QuanLyKhachSan.Web.Pages.Rooms.RoomList>();
            return System.Net.WebUtility.HtmlDecode(result.ToHtmlString());
        });
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<article").Cast<System.Text.RegularExpressions.Match>());
        Assert.Contains("Standard", html);
        Assert.Contains(">Twin</button>", html);
        Assert.Contains(">Double</button>", html);
        Assert.DoesNotContain("room-categories", html);
        Assert.Contains("Chỉ còn 3 phòng", html);
        Assert.DoesNotContain("SECRET", html);
        Assert.DoesNotContain("River", html);
        Assert.DoesNotContain("City", html);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/rooms");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7L)]
    public async Task Services_are_public_information_without_booking_forms(long? id)
    {
        using var http = new HttpClient(new ServiceHandler()) { BaseAddress = new Uri("http://localhost/") };
        var api = new QuanLyKhachSan.Web.Services.ApiClient(http, new QuanLyKhachSan.Web.State.AuthState(null!),
            Microsoft.Extensions.Options.Options.Create(new QuanLyKhachSan.Web.Services.ApiOptions { UseMockData = false }));
        using var services = new ServiceCollection().AddLogging().AddSingleton(api).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => {
            var result = await renderer.RenderComponentAsync<QuanLyKhachSan.Web.Pages.Services>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = id }));
            return System.Net.WebUtility.HtmlDecode(result.ToHtmlString());
        });
        Assert.Contains("Bữa sáng", html);
        Assert.Contains("images/services/dining.jpg", html);
        Assert.DoesNotContain("<input", html);
        Assert.DoesNotContain("<select", html);
        Assert.DoesNotContain("Yêu cầu dịch vụ", html);
        if (id is null) Assert.Contains("services/7", html);
        else Assert.Contains("Buffet mỗi sáng", html);
    }
    private sealed class ServiceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.StartsWith("/v1/services?", request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent("""{"items":[{"id":7,"ten_dich_vu":"Bữa sáng","don_vi_tinh":"suất","don_gia":150000,"mo_ta":"Buffet mỗi sáng"}]}""")
            });
        }
    }
}
