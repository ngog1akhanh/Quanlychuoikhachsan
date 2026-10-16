using System.ComponentModel.DataAnnotations;

namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>Thông tin trả về cho tài khoản tạo QR hoặc tra cứu trạng thái đơn của mình.</summary>
public sealed record VietQrPaymentResponse(
    string OrderId,
    long InvoiceId,
    decimal Amount,
    string Description,
    string QrCode,
    string QrDataUrl,
    string Status,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? TransactionId = null,
    string? TransactionDateTime = null);

/// <summary>Thông tin đơn thanh toán dùng trong màn hình quản trị.</summary>
public sealed record VietQrPaymentListResponse(
    long Id,
    string OrderId,
    long InvoiceId,
    decimal Amount,
    string Description,
    string QrCode,
    string QrDataUrl,
    string Status,
    string? TransactionId,
    string? Note,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? TransactionDateTime = null);

/// <summary>Yêu cầu cập nhật ghi chú nội bộ của đơn VietQR.</summary>
public sealed record UpdateVietQrPaymentNoteRequest(
    [param: StringLength(1000)] string? Note);

/// <summary>Yêu cầu đổi trạng thái cục bộ; service không chấp nhận PAID/REFUNDED.</summary>
public sealed record UpdateVietQrPaymentStatusRequest(
    [param: Required] string Status);

/// <summary>Envelope phản hồi thống nhất cho các endpoint thanh toán.</summary>
public sealed record PaymentApiResponse<T>(bool Success, string Message, T? Data, string? RequestId = null);
