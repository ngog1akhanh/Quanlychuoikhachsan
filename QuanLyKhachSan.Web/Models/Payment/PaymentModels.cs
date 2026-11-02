namespace QuanLyKhachSan.Web.Models.Payment;

/// <summary>Phương thức thanh toán (khách đặt cọc bằng VietQR).</summary>
public sealed record PaymentMethodItem(long Id, string Code, string Name);

/// <summary>Chỉ dùng ở chế độ mock (API thật yêu cầu quyền thu ngân cho confirm-deposit).</summary>
public sealed record ConfirmDepositRequest(long PaymentMethodId, decimal Amount, string PaymentGateway, string TransactionId);

/// <summary>Envelope của nhóm API thanh toán VietQR.</summary>
public sealed record PaymentApiResponse<T>(bool Success, string Message, T? Data, string? RequestId);

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
    string? TransactionId,
    string? TransactionDateTime);

/// <summary>Dữ liệu hiển thị ở trang kết quả thanh toán.</summary>
public sealed record PaymentResultInfo(bool Success, string BookingCode, decimal Amount, string TransactionId, string Message);

/// <summary>
/// Đơn cọc VietQR của khách: POST /v1/bookings/{id}/deposit-payment và POST /v1/deposit-payments/{orderId}/inquiry.
/// Status: PENDING | PAID | FAILED | REFUND_REQUIRED | REFUNDED. Số tiền cọc do server tính.
/// HoldExpiresAt: hạn giữ phòng mới nhất (server nới khi tạo QR). Demo: API đang chạy cổng VietQR giả lập.
/// </summary>
public sealed record DepositOrder(
    string OrderId,
    long BookingId,
    decimal Amount,
    string Description,
    string? QrCode,
    string? QrDataUrl,
    string Status,
    string? TransactionId,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? BookingCode = null,
    DateTime? HoldExpiresAt = null,
    bool Demo = false);
