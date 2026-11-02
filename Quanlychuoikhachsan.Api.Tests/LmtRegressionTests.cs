using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Controllers;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Housekeeping;
using Quanlychuoikhachsan.Application.Reports;
using Quanlychuoikhachsan.Domain.Entities;
using System.Text.Json;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class LmtRegressionTests
{
    [Fact]
    public async Task Serious_condition_keeps_an_occupied_room_occupied()
    {
        await using var store = new TestStore();
        await store.SeedAsync();
        await store.SeedStayAsync();
        (await store.Db.Phongs.SingleAsync(x => x.Id == 1)).TrangThai = "DANG_O";
        await store.Db.SaveChangesAsync();
        var service = new HousekeepingService(store.Db, store.Actor);
        await service.RecordConditionAsync(new(1, 1, null, "HU_HONG", "NGHIEM_TRONG", "Test incident", 0, 0, false, null), default);
        Assert.Equal("DANG_O", (await store.Db.Phongs.SingleAsync(x => x.Id == 1)).TrangThai);
        await service.RecordConditionAsync(new(2, null, null, "HU_HONG", "NGHIEM_TRONG", "Empty room incident", 0, 0, false, null), default);
        Assert.Equal("BAO_TRI", (await store.Db.Phongs.SingleAsync(x => x.Id == 2)).TrangThai);
    }

    [Fact]
    public async Task Revenue_includes_vietnam_midnight_and_excludes_next_day()
    {
        await using var store = new TestStore();
        await store.SeedAsync();
        await store.SeedStayAsync();
        var date = new DateOnly(2030, 1, 2);
        var start = new DateTime(2030, 1, 1, 17, 0, 0, DateTimeKind.Utc);
        foreach (var (id, time, amount) in new[] { (1L, start.AddTicks(-10), 99m), (2L, start, 100m), (3L, start.AddHours(6), 200m), (4L, start.AddDays(1), 999m) })
            store.Db.HoaDonPhongs.Add(new HoaDonPhong { Id = id, MaHoaDon = $"TZ{id}", DatPhongId = 1, NgayLap = time, TienPhong = amount, TongTien = amount, TrangThai = "DA_PHAT_HANH" });
        await store.Db.SaveChangesAsync();
        var report = await new ReportService(store.Db).RevenueAsync(new(date, date), default);
        Assert.Equal(300m, report.TotalRevenue);
        var action = await new OperationalReportsController(store.Db).Revenue(date, date, default);
        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(action).Value);
        Assert.Equal(300m, json.GetProperty("Daily")[0].GetProperty("Rooms").GetDecimal());
    }
}
