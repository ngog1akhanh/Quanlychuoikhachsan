namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>Dữ liệu server gửi tới VietQR để tạo QR gắn với hóa đơn.</summary>
public sealed record VietQrCreateCustomerQrRequest(
    long Amount,
    string Content,
    string BankAccount,
    string BankCode,
    string UserBankName,
    string TransType,
    string OrderId,
    int QrType);

/// <summary>Kết quả tạo QR do VietQR trả về.</summary>
public sealed record VietQrCustomerQrResult(string QrCode, string QrDataUrl);

/// <summary>Kết quả Inquiry dùng để đối chiếu trước khi ghi nhận thanh toán.</summary>
public sealed record VietQrInquiryResult(
    string Code,
    string Description,
    string OrderId,
    decimal Amount,
    string? TransactionId,
    string Status,
    string? TransactionDescription,
    string? TransactionDateTime);

/// <summary>Hợp đồng giao tiếp với API VietQR, tách khỏi nghiệp vụ thanh toán.</summary>
public interface IVietQrGateway
{
    Task<VietQrCustomerQrResult> CreateCustomerQrAsync(
        VietQrCreateCustomerQrRequest request,
        CancellationToken cancellationToken);

    Task<VietQrInquiryResult> InquireAsync(string orderId, CancellationToken cancellationToken);
}
