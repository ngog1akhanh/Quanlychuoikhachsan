using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Điều phối vòng đời đặt phòng từ tạo giữ chỗ đến nhận phòng, trả phòng hoặc hủy.</summary>
[Route("v1/bookings")]
[Authorize]
public sealed class BookingsController(IBookingService bookingService) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<ApiResponse<BookingResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Create(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        var booking = await bookingService.CreateBookingAsync(accountId, request, cancellationToken);
        return Created($"/v1/{booking.Name}", booking);
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingResponse>> Patch(
        long id,
        PatchBookingRequest request,
        CancellationToken cancellationToken)
        => OkResponse(await bookingService.UpdateBookingAsync(GetAccountId(), id, request, cancellationToken));

    [HttpPost("{id:long}:confirm-deposit")]
    [RequirePermission("BILLING_PAYMENT_WRITE")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingResponse>> ConfirmDeposit(
        long id,
        ConfirmDepositRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await bookingService.ConfirmDepositAsync(GetAccountId(), id, request, cancellationToken);
        return OkResponse(booking);
    }

    /// <summary>Registers guest arrival and creates the initial room invoice snapshot.</summary>
    [HttpPost("{id:long}:check-in")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<InvoiceResponse>> CheckIn(long id, CheckInRequest request, CancellationToken ct)
        => OkResponse(await bookingService.CheckInAsync(id, request, ct));

    /// <summary>Registers departure and finalizes room, surcharge, compensation, and VAT amounts.</summary>
    [HttpPost("{id:long}:check-out")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<InvoiceResponse>> CheckOut(long id, CheckOutRequest request, CancellationToken ct)
        => OkResponse(await bookingService.CheckOutAsync(id, request, ct));

    [HttpPost("{id:long}:cancel")]
    public async Task<ActionResult<CancellationRequestResponse>> Cancel(long id, CancelBookingRequest request, CancellationToken ct)
        => OkResponse(await bookingService.CancelAsync(GetAccountId(), id, request, ct));

    [HttpPost("{id:long}:mark-no-show")]
    [RequirePermission("BOOKING_WRITE")]
    public async Task<ActionResult<BookingResponse>> MarkNoShow(long id, MarkNoShowRequest request, CancellationToken ct)
        => OkResponse(await bookingService.MarkNoShowAsync(id, request, ct));

    private long GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(value, out var accountId))
            throw new UnauthenticatedException("The authenticated token does not contain a valid account id.");
        return accountId;
    }
}
