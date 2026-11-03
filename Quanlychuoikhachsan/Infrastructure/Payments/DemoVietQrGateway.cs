using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Payments;

namespace Quanlychuoikhachsan.Infrastructure.Payments;

/// <summary>
/// Cổng VietQR giả lập cho môi trường demo (chưa có tài khoản VietQR sandbox).
/// Tự dựng chuỗi QR theo chuẩn VietQR/EMVCo (app ngân hàng đọc được số tiền, nội dung)
/// nhưng không có giao dịch thật: tra cứu luôn trả "PENDING".
/// Xác nhận đã nhận tiền bằng API giả lập: POST /v1/deposit-payments/{orderId}:simulate-paid.
/// </summary>
public sealed class DemoVietQrGateway(IOptions<VietQrSettings> settings) : IVietQrGateway
{
    private const string DefaultBin = "970436";          // Vietcombank
    private const string DefaultAccount = "0123456789";  // tài khoản minh họa
    private static readonly ConcurrentDictionary<string, long> Amounts = new();

    public Task<VietQrCustomerQrResult> CreateCustomerQrAsync(VietQrCreateCustomerQrRequest request, CancellationToken cancellationToken)
    {
        var s = settings.Value;
        var bin = Digits(s.BankBin) is { Length: 6 } b ? b : DefaultBin;
        var account = Digits(string.IsNullOrWhiteSpace(request.BankAccount) ? s.BankAccount : request.BankAccount) is { Length: > 0 } a ? a : DefaultAccount;
        Amounts[request.OrderId] = request.Amount;
        var payload = BuildPayload(bin, account, request.Amount, request.Content);
        // Không có ảnh từ VietQR: web tự vẽ QR từ qr_code.
        return Task.FromResult(new VietQrCustomerQrResult(payload, string.Empty));
    }

    public Task<VietQrInquiryResult> InquireAsync(string orderId, CancellationToken cancellationToken)
        => Task.FromResult(new VietQrInquiryResult("00", "DEMO", orderId,
            Amounts.TryGetValue(orderId, out var amount) ? amount : 0, null, "PENDING", null, null));

    /// <summary>Chuỗi VietQR (NAPAS 247, chuyển nhanh đến tài khoản) có số tiền và nội dung.</summary>
    public static string BuildPayload(string bin, string account, long amount, string content)
    {
        var beneficiary = Field("00", bin) + Field("01", account);
        var merchant = Field("00", "A000000727") + Field("01", beneficiary) + Field("02", "QRIBFTTA");
        var purpose = Ascii(content, 25);
        var body = new StringBuilder()
            .Append(Field("00", "01"))
            .Append(Field("01", "12"))
            .Append(Field("38", merchant))
            .Append(Field("53", "704"))
            .Append(Field("54", amount.ToString(CultureInfo.InvariantCulture)))
            .Append(Field("58", "VN"));
        if (purpose.Length > 0) body.Append(Field("62", Field("08", purpose)));
        body.Append("6304");
        var text = body.ToString();
        return text + Crc16(text).ToString("X4", CultureInfo.InvariantCulture);
    }

    private static string Field(string id, string value) => id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    private static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    private static string Ascii(string value, int max)
    {
        var chars = value.Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray();
        var text = new string(chars).Trim();
        return text.Length > max ? text[..max].TrimEnd() : text;
    }

    // CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) theo EMVCo.
    private static ushort Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
