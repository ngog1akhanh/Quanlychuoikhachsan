using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tạo QR hóa đơn và truy vấn trạng thái qua API VietQR đã xác thực.</summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class VietQrPaymentsController(IVietQrPaymentService paymentService) : ApiControllerBase
{
    // Tạo đơn QR cho hóa đơn do tài khoản hiện tại sở hữu; số tiền do service tính từ database.
    [HttpPost("invoices/{invoiceId:long}/vietqr-payment")]
    [ProducesResponseType<PaymentApiResponse<VietQrPaymentResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PaymentApiResponse<VietQrPaymentResponse>>> CreateInvoicePayment(
        long invoiceId,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.CreateInvoicePaymentAsync(
            GetAccountId(), invoiceId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            new PaymentApiResponse<VietQrPaymentResponse>(true, "VietQR payment QR created.", payment, RequestId));
    }

    // Gọi Inquiry cho đơn của tài khoản hiện tại, không nhận trạng thái thanh toán từ client.
    [HttpPost("payments/vietqr/{orderId}/inquiry")]
    [ProducesResponseType<PaymentApiResponse<VietQrPaymentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentApiResponse<VietQrPaymentResponse>>> InquirePayment(
        string orderId,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.InquirePaymentAsync(
            GetAccountId(), orderId, cancellationToken);
        return Ok(new PaymentApiResponse<VietQrPaymentResponse>(
            true, "VietQR payment status retrieved.", payment, RequestId));
    }

    // Đọc account ID từ claim đã xác thực để áp dụng kiểm tra quyền sở hữu ở service.
    private long GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(value, out var accountId))
            throw new UnauthenticatedException("The authenticated token does not contain a valid account id.");
        return accountId;
    }
}
