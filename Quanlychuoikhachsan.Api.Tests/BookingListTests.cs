using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.FrontDesk;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class BookingListTests
{
    [Fact]
    public async Task Codes_use_database_ids_and_list_orders_dates_before_pagination()
    {
        await using var store = new TestStore();
        await store.SeedAsync();
        var day = HotelTime.Today.AddDays(20);
        async Task<BookingResponse> Create(int offset) => await store.Bookings.CreateBookingAsync(2,
            new([1], HotelTime.ToUtc(day.AddDays(offset).ToDateTime(new TimeOnly(14, 0))),
                HotelTime.ToUtc(day.AddDays(offset + 1).ToDateTime(new TimeOnly(12, 0)))), default);
        var later = await Create(40);
        var earlier = await Create(0);
        Assert.Equal($"DP-{later.Id:D4}", later.BookingCode);
        Assert.Equal($"DP-{earlier.Id:D4}", (await store.Db.DatPhongs.SingleAsync(x => x.Id == earlier.Id)).MaDatPhong);
        var front = new FrontDeskService(store.Db, store.Bookings, store.Actor);
        var first = await front.SearchAsync(new(Page: 1, PageSize: 1), default);
        var second = await front.SearchAsync(new(Page: 2, PageSize: 1), default);
        Assert.Equal(later.Id, Assert.Single(first.Items).Id);
        Assert.Equal(earlier.Id, Assert.Single(second.Items).Id);
        var ascFirst = await front.SearchAsync(new(Page: 1, PageSize: 1, ArrivalDescending: false), default);
        var ascSecond = await front.SearchAsync(new(Page: 2, PageSize: 1, ArrivalDescending: false), default);
        Assert.Equal(earlier.Id, Assert.Single(ascFirst.Items).Id);
        Assert.Equal(later.Id, Assert.Single(ascSecond.Items).Id);
        var found = await front.SearchAsync(new(Q: earlier.BookingCode), default);
        Assert.Equal(earlier.Id, Assert.Single(found.Items).Id);
    }
}
