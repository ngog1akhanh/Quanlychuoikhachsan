using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.FrontDesk;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>
/// API lễ tân cho App quản lý (Giai đoạn 1): đặt phòng tại quầy, đổi ngày/đổi phòng, hủy, no-show,
/// danh sách khách đến/đi, khai báo lưu trú, nhận/trả phòng, duyệt yêu cầu hủy.
/// Quyền: BOOKING_READ, BOOKING_WRITE, BOOKING_WRITE, CANCELLATION_APPROVE (seed_api_permissions.sql).
/// </summary>
[ApiController]
[Authorize]
[Route("v1/front-desk")]
public sealed class FrontDeskController(IFrontDeskService frontDesk) : ApiControllerBase
{
    // ---------------- Tra cứu ----------------
    [HttpGet("bookings")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<FrontDeskBookingPage>> Search([FromQuery] FrontDeskBookingQuery query, CancellationToken ct)
        => OkResponse(await frontDesk.SearchAsync(query, ct));

    [HttpGet("bookings/{id:long}")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<FrontDeskBookingDetail>> Get(long id, CancellationToken ct)
        => OkResponse(await frontDesk.GetDetailAsync(id, ct));

    [HttpGet("available-rooms")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<IReadOnlyList<AvailableRoom>>> AvailableRooms(
        [FromQuery] DateOnly checkIn, [FromQuery] DateOnly checkOut, [FromQuery] long? excludeBookingId, CancellationToken ct)
        => OkResponse(await frontDesk.FindAvailableRoomsAsync(checkIn, checkOut, excludeBookingId, ct));

    [HttpGet("customers")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<IReadOnlyList<FrontDeskCustomer>>> Customers([FromQuery] string? q, CancellationToken ct)
        => OkResponse(await frontDesk.FindCustomersAsync(q, ct));

    [HttpGet("payment-methods")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodItem>>> PaymentMethods(CancellationToken ct)
        => OkResponse(await frontDesk.ListPaymentMethodsAsync(ct));

    [HttpGet("day")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<FrontDeskDay>> Day([FromQuery] DateOnly? date, CancellationToken ct)
        => OkResponse(await frontDesk.GetDayAsync(date ?? HotelTime.Today, ct));

    // ---------------- Tạo và thay đổi booking ----------------
    [HttpPost("bookings")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<FrontDeskBookingDetail>> Create(CreateDeskBookingRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.CreateAsync(GetAccountId(), request, ct));

    [HttpPost("bookings/{id:long}:confirm-deposit")]
    [RequirePermission("BILLING_PAYMENT_WRITE")]
    public async Task<ActionResult<FrontDeskBookingDetail>> ConfirmDeposit(long id, DeskDepositRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.ConfirmDepositAsync(GetAccountId(), id, request, ct));

    [HttpPost("bookings/{id:long}:change-dates")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<ChangeResult>> ChangeDates(long id, ChangeDatesRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.ChangeDatesAsync(GetAccountId(), id, request, ct));

    [HttpPost("bookings/{id:long}:change-room")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<ChangeResult>> ChangeRoom(long id, ChangeRoomRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.ChangeRoomAsync(GetAccountId(), id, request, ct));

    [HttpPost("bookings/{id:long}:cancel")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<ChangeResult>> Cancel(long id, DeskCancelRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.CancelAsync(GetAccountId(), id, request, ct));

    [HttpPost("bookings/{id:long}:mark-no-show")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<FrontDeskBookingDetail>> NoShow(long id, DeskNoteRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.MarkNoShowAsync(GetAccountId(), id, request, ct));

    // ---------------- Nhận / trả phòng ----------------
    [HttpPut("bookings/{id:long}/guests")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<FrontDeskBookingDetail>> SaveGuests(long id, IReadOnlyList<StayGuestDto> guests, CancellationToken ct)
        => OkResponse(await frontDesk.SaveGuestsAsync(id, guests, ct));

    [HttpPost("bookings/{id:long}:check-in")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<FrontDeskBookingDetail>> CheckIn(long id, DeskCheckInRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.CheckInAsync(GetAccountId(), id, request, ct));

    [HttpGet("bookings/{id:long}/checkout-preview")]
    [RequirePermission("BILLING_READ")]
    public async Task<ActionResult<CheckOutPreview>> CheckOutPreview(long id, CancellationToken ct)
        => OkResponse(await frontDesk.PreviewCheckOutAsync(id, ct));

    [HttpPost("bookings/{id:long}:check-out")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<DeskCheckOutResult>> CheckOut(long id, DeskCheckOutRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.CheckOutAsync(GetAccountId(), id, request, ct));

    // ---------------- Yêu cầu hủy ----------------
    [HttpGet("cancellation-requests")]
    [RequirePermission("BOOKING_READ")]
    public async Task<ActionResult<IReadOnlyList<CancellationRequestItem>>> Cancellations([FromQuery] string? status, CancellationToken ct)
        => OkResponse(await frontDesk.ListCancellationsAsync(status, ct));

    [HttpPost("cancellation-requests/{id:long}:approve")]
    [RequirePermission("CANCELLATION_APPROVE")]
    public async Task<ActionResult<CancellationRequestItem>> Approve(long id, DecideCancellationRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.ApproveCancellationAsync(GetAccountId(), id, request, ct));

    [HttpPost("cancellation-requests/{id:long}:reject")]
    [RequirePermission("CANCELLATION_APPROVE")]
    public async Task<ActionResult<CancellationRequestItem>> Reject(long id, DeskNoteRequest request, CancellationToken ct)
        => OkResponse(await frontDesk.RejectCancellationAsync(GetAccountId(), id, request, ct));

    private long GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : throw new UnauthenticatedException("Token không chứa mã tài khoản hợp lệ.");
    }
}
