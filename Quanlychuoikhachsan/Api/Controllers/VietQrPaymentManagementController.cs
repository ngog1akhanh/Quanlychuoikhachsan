using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Cho phép người có quyền xem yêu cầu VietQR và quản lý ghi chú/trạng thái.</summary>
[ApiController]
[Authorize]
[Route("v1/payments/vietqr")]
public sealed class VietQrPaymentManagementController(IVietQrPaymentService paymentService) : ApiControllerBase
{
    // Danh sách đơn theo trạng thái, giới hạn và phân quyền do service/permission kiểm soát.
    [HttpGet]
    [RequirePermission("VIETQR_PAYMENT_READ")]
    public async Task<ActionResult<PaymentApiResponse<IReadOnlyList<VietQrPaymentListResponse>>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var payments = await paymentService.ListPaymentOrdersAsync(status, cancellationToken);
        return Ok(new PaymentApiResponse<IReadOnlyList<VietQrPaymentListResponse>>(
            true, "Payment orders retrieved.", payments, RequestId));
    }

    // Cập nhật ghi chú phụ trợ, không thay đổi kết quả đối soát.
    [HttpPut("{orderId}/note")]
    [RequirePermission("VIETQR_PAYMENT_NOTE_WRITE")]
    public async Task<ActionResult<PaymentApiResponse<VietQrPaymentListResponse>>> UpdateNote(
        string orderId,
        UpdateVietQrPaymentNoteRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.UpdatePaymentNoteAsync(orderId, request.Note, cancellationToken);
        return Ok(new PaymentApiResponse<VietQrPaymentListResponse>(
            true, "Payment note updated.", payment, RequestId));
    }

    // Cập nhật trạng thái cục bộ; service từ chối xác nhận PAID hoặc REFUNDED thủ công.
    [HttpPatch("{orderId}/status")]
    [RequirePermission("VIETQR_PAYMENT_STATUS_WRITE")]
    public async Task<ActionResult<PaymentApiResponse<VietQrPaymentListResponse>>> UpdateStatus(
        string orderId,
        UpdateVietQrPaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.UpdatePaymentStatusAsync(orderId, request.Status, cancellationToken);
        return Ok(new PaymentApiResponse<VietQrPaymentListResponse>(
            true, "Payment status updated.", payment, RequestId));
    }
}
