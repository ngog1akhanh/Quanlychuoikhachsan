using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelAdmin.Models;
using HotelAdmin.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;
using ApiException = HotelAdmin.Services.ApiException;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class WinFormsCompatibilityTests
{
    private static async Task GrantDeskAsync(ApiFactory factory)
    {
        await factory.SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
        db.Add(new VaiTro { Id = 2, MaVaiTro = "RECEPTIONIST", TenVaiTro = "Reception", TrangThai = "HOAT_DONG" });
        long id = 100;
        foreach (var code in new[] { "BOOKING_READ", "BOOKING_WRITE", "ROOM_READ", "ROOM_WRITE", "ROOM_TYPE_READ", "ROOM_TYPE_WRITE", "BILLING_READ", "BILLING_WRITE", "BILLING_PAYMENT_WRITE", "CANCELLATION_APPROVE" })
        {
            db.Add(new Quyen { Id = id, MaQuyen = code, TenQuyen = code });
            db.Add(new VaiTroQuyen { VaiTroId = 2, QuyenId = id++ });
        }
        db.Add(new TaiKhoanVaiTro { TaiKhoanId = 1, VaiTroId = 2 });
        await db.SaveChangesAsync();
    }

    private static (HttpClient Http, ApiClient Api) Client(ApiFactory factory, long account = 1)
    {
        var http = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost/") });
        ApiFactory.SignIn(http, account);
        return (http, new ApiClient(http, http.BaseAddress!));
    }

    private static CreateDeskBookingRequest Booking(DateOnly? day = null, DeskDepositRequest? deposit = null)
    {
        var date = day ?? HotelTime.Today;
        return new(1, null, [2], date.ToDateTime(new TimeOnly(14, 0)), date.AddDays(2).ToDateTime(new TimeOnly(12, 0)),
            1, 0, "QUAY", null, deposit);
    }

    [Fact]
    public async Task Me_supports_staff_menu_and_preserves_customer_profile_fields()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (http, client) = Client(f);
        var staff = await client.GetMeAsync();
        Assert.Equal(1, staff.AccountId);
        Assert.Equal(1, staff.StaffId);
        Assert.Contains("BOOKING_WRITE", staff.Permissions);
        Assert.Contains(staff.Roles, x => x.Code == "RECEPTIONIST");
        ApiFactory.SignIn(http, 2);
        var customer = await http.GetFromJsonAsync<JsonElement>("v1/me");
        Assert.Equal(1, customer.GetProperty("id").GetInt64());
        Assert.Equal("Guest", customer.GetProperty("full_name").GetString());
        Assert.Equal(2, (await client.GetMeAsync()).AccountId);
    }

    [Fact]
    public async Task Real_WinForms_client_completes_one_invoice_checkout_without_duplicate_money_or_tasks()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (_, client) = Client(f);
        var booking = await client.CreateDeskBookingAsync(Booking(deposit: new(1, 60, Reference: "create-deposit")));
        Assert.Equal("BOOKED", booking.Summary.Status);
        Assert.Equal(HotelTime.Today.ToDateTime(new TimeOnly(14, 0)), booking.Summary.CheckIn);
        Assert.Contains((await client.SearchBookingsAsync(null, "BOOKED", null, null, 1, 50)).Items, x => x.Id == booking.Summary.Id);
        var guest = new StayGuestDto(null, booking.Rooms[0].DetailId, "Guest", null, null, "CCCD", "123456789", "VN", true);
        booking = await client.CheckInAsync(booking.Summary.Id, new([guest]));
        var draft = Assert.Single(booking.Invoices);
        Assert.Equal("NHAP", draft.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAsync<ApiException>(() => client.CheckOutAsync(booking.Summary.Id, new(0)))).StatusCode);
        var quote = await client.GetCheckOutPreviewAsync(booking.Summary.Id);
        var issued = await client.PrepareInvoiceAsync(booking.Summary.Id, 0, 0.1m);
        Assert.Equal(draft.Id, issued.Id);
        Assert.Equal(quote.EstimatedTotal, issued.Total);
        var payment = new InvoicePayment(1, issued.Due, "one-payment");
        await client.PayInvoiceAsync(issued.Id, payment);
        await client.PayInvoiceAsync(issued.Id, payment);
        var result = await client.CheckOutAsync(booking.Summary.Id, new(0));
        Assert.Equal(0, result.Invoice.Due);
        Assert.Equal(draft.Id, result.Invoice.Id);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
        Assert.Equal(1, await db.HoaDonPhongs.CountAsync(x => x.DatPhongId == booking.Summary.Id));
        Assert.Equal(1, await db.ThanhToanHoaDonPhongs.CountAsync(x => x.HoaDonPhongId == issued.Id));
        Assert.Equal(1, await db.CongViecBuongPhongs.CountAsync(x => x.DatPhongId == booking.Summary.Id));
        Assert.Equal(1, await db.Set<LichSuTrangThaiDatPhong>().CountAsync(x => x.DatPhongId == booking.Summary.Id && x.TrangThaiMoi == "DA_TRA"));
    }

    [Fact]
    public async Task Legacy_deposit_retry_is_idempotent_and_changed_payload_is_rejected()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (_, client) = Client(f);
        var booking = await client.CreateDeskBookingAsync(Booking());
        await client.ConfirmDepositAsync(booking.Summary.Id, new(1, 60));
        var repeated = await client.ConfirmDepositAsync(booking.Summary.Id, new(1, 60));
        Assert.Single(repeated.Deposits);
        Assert.Equal(HttpStatusCode.Conflict, (await Assert.ThrowsAsync<ApiException>(() => client.ConfirmDepositAsync(booking.Summary.Id, new(1, 70)))).StatusCode);
        var topup = await client.ConfirmDepositAsync(booking.Summary.Id, new(1, 20, Reference: "second-receipt"));
        Assert.Equal(2, topup.Deposits.Count);
    }

    [Fact]
    public async Task Legacy_cancel_and_new_approval_share_refund_workflow()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
            db.Add(new ChinhSachHuy { Id = 1, TenChinhSach = "Refund" });
            db.Add(new ChiTietChinhSachHuy { ChinhSachHuyId = 1, SoGioTruocToiThieu = 0, PhanTramHoan = 100 });
            await db.SaveChangesAsync();
        }
        var (http, client) = Client(f);
        var booking = await client.CreateDeskBookingAsync(Booking(HotelTime.Today.AddDays(2), new(1, 60)));
        var cancelled = await client.CancelBookingAsync(booking.Summary.Id, "Change of plan");
        var request = Assert.Single(cancelled.Booking.Cancellations);
        Assert.Equal(60, request.ExpectedRefund);
        var approved = await http.PostAsJsonAsync($"v1/cancellation-requests/{request.Id}:approve", new { employee_id = 999, approved_refund = 60, note = "Approved" });
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        Assert.Equal("DA_HUY", (await client.GetBookingDetailAsync(booking.Summary.Id)).Summary.Status);
        await using var finalScope = f.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<HotelDbContext>();
        var refund = Assert.Single(await finalDb.Set<PhieuHoanTien>().ToListAsync());
        Assert.Equal(60, refund.SoTien);
        Assert.Equal("CHO_XU_LY", refund.TrangThai);
        Assert.Empty(await finalDb.KhoaLichPhongs.ToListAsync());
    }

    [Fact]
    public async Task Both_room_status_routes_reject_blocking_a_reserved_room()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (http, client) = Client(f);
        await client.CreateDeskBookingAsync(Booking(HotelTime.Today.AddDays(2)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAsync<ApiException>(() => client.SetRoomStatusAsync(2, "BAO_TRI", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PatchAsJsonAsync("v1/rooms/2/status", new { status = "BAO_TRI" })).StatusCode);
        var updated = await client.UpdateRoomAsync(1, new(RoomNumber: "103", NightSurcharge: 12));
        Assert.Equal("103", updated.RoomNumber);
        Assert.Equal(12, updated.NightSurcharge);
        Assert.NotEmpty(await client.GetRoomTypeSummariesAsync());
        Assert.NotEmpty((await client.GetRoomCalendarAsync(HotelTime.Today, HotelTime.Today.AddDays(5))).Cells);
    }

    [Fact]
    public async Task Guest_limits_and_non_ready_room_cannot_be_bypassed_through_legacy_routes()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (_, client) = Client(f);
        var booking = await client.CreateDeskBookingAsync(Booking(deposit: new(1, 60)));
        var guest = new StayGuestDto(null, booking.Rooms[0].DetailId, "Guest", null, null, "CCCD", "123", null, true);
        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAsync<ApiException>(() => client.SaveGuestsAsync(booking.Summary.Id, [guest, guest]))).StatusCode);
        await client.SetRoomStatusAsync(2, "CAN_DON", null);
        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAsync<ApiException>(() => client.CheckInAsync(booking.Summary.Id, new(null, true)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAsync<ApiException>(() => client.CheckInAsync(booking.Summary.Id, new(null)))).StatusCode);
        Assert.Empty((await client.GetBookingDetailAsync(booking.Summary.Id)).Invoices);
    }

    [Fact]
    public async Task Local_midnight_keeps_the_correct_nightly_dates_and_UTC_storage()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (http, client) = Client(f);
        var day = HotelTime.Today.AddDays(3);
        var request = Booking(day) with { CheckIn = day.ToDateTime(new TimeOnly(1, 0)) };
        var booking = await client.CreateDeskBookingAsync(request);
        Assert.Equal(request.CheckIn, booking.Summary.CheckIn);
        Assert.Equal(day, booking.Nights.Min(x => x.StayDate));
        Assert.Contains((await client.GetFrontDeskDayAsync(day)).Arrivals, x => x.Id == booking.Summary.Id);
        var modern = await http.GetFromJsonAsync<JsonElement>($"v1/bookings/{booking.Summary.Id}");
        Assert.Equal(HotelTime.ToUtc(request.CheckIn), DateTime.SpecifyKind(modern.GetProperty("du_kien_nhan").GetDateTime(), DateTimeKind.Utc));
    }

    [Fact]
    public async Task Customer_is_denied_staff_routes_even_with_valid_token()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        var (_, client) = Client(f, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() => client.GetBookingDetailAsync(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() => client.GetRoomBoardAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() => client.CreateDeskBookingAsync(Booking()))).StatusCode);
    }

    [Fact]
    public async Task Booking_writer_cannot_bypass_cashier_permission_using_legacy_endpoints()
    {
        await using var f = new ApiFactory();
        await GrantDeskAsync(f);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
            var permission = await db.Quyens.SingleAsync(x => x.MaQuyen == "BILLING_PAYMENT_WRITE");
            db.RemoveRange(await db.VaiTroQuyens.Where(x => x.QuyenId == permission.Id).ToListAsync());
            await db.SaveChangesAsync();
        }
        var (_, client) = Client(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() => client.CreateDeskBookingAsync(Booking(deposit: new(1, 60))))).StatusCode);
        var booking = await client.CreateDeskBookingAsync(Booking());
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() => client.ConfirmDepositAsync(booking.Summary.Id, new(1, 60)))).StatusCode);
        Assert.Empty((await client.GetBookingDetailAsync(booking.Summary.Id)).Deposits);
    }

    [Theory]
    [InlineData("LE_TAN", "RECEPTIONIST")]
    [InlineData("BUONG_PHONG", "HOUSEKEEPING")]
    [InlineData("DICH_VU", "SERVICE")]
    [InlineData("KHO", "WAREHOUSE")]
    public void Old_and_new_role_codes_show_the_same_menu(string legacy, string canonical)
    {
        var session = new UserSession(new MeResponse(1, "Staff", "NHAN_VIEN", null, 1, "NV1", [new(legacy, "Staff")], []), DateTime.UtcNow.AddHours(1));
        Assert.True(session.HasRole(canonical));
        Assert.True(session.HasRole(legacy));
        Assert.False(session.HasRole("ADMIN"));
    }
}
