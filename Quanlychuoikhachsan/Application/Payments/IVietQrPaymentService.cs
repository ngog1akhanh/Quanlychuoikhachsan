namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>Nghiệp vụ tạo QR hóa đơn, Inquiry và quản lý đơn VietQR.</summary>
public interface IVietQrPaymentService
{
    Task<VietQrPaymentResponse> CreateInvoicePaymentAsync(
        long accountId,
        long invoiceId,
        CancellationToken cancellationToken);

    Task<VietQrPaymentResponse> InquirePaymentAsync(
        long accountId,
        string orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VietQrPaymentListResponse>> ListPaymentOrdersAsync(
        string? status,
        CancellationToken cancellationToken);

    Task<VietQrPaymentListResponse> UpdatePaymentNoteAsync(
        string orderId,
        string? note,
        CancellationToken cancellationToken);

    Task<VietQrPaymentListResponse> UpdatePaymentStatusAsync(
        string orderId,
        string status,
        CancellationToken cancellationToken);
}
