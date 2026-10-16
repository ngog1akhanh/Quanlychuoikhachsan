using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Xử lý phê duyệt hủy đặt phòng và ghi nhận thanh toán hóa đơn.</summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class BillingActionsController(IBookingService bookingService) : ApiControllerBase
{
    [HttpPost("cancellation-requests/{id:long}:approve")]
    public async Task<ActionResult<CancellationRequestResponse>> ApproveCancellation(long id, ApproveCancellationRequest request, CancellationToken ct)
        => OkResponse(await bookingService.ApproveCancellationAsync(id, request, ct));

    [HttpPost("invoices/{id:long}:pay")]
    [RequirePermission("BILLING_PAYMENT_WRITE")]
    public async Task<ActionResult<InvoiceResponse>> PayInvoice(long id, AddInvoicePaymentRequest request, CancellationToken ct)
        => OkResponse(await bookingService.AddInvoicePaymentAsync(id, request, ct));
}