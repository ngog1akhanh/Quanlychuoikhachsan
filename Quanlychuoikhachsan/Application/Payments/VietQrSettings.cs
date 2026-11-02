namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>
/// Cấu hình VietQR (mục "VietQR" trong appsettings / User Secrets).
/// Mode: "Auto" (mặc định) – có đủ tài khoản sandbox thì gọi VietQR thật, thiếu thì chạy demo;
/// "Demo" – luôn demo; "Live" – luôn gọi VietQR thật.
/// </summary>
public sealed class VietQrSettings
{
    public const string SectionName = "VietQR";

    public string Mode { get; set; } = "Auto";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string UserBankName { get; set; } = string.Empty;

    /// <summary>Mã BIN ngân hàng (6 số) dùng để dựng mã QR ở chế độ demo, vd. 970436 = Vietcombank.</summary>
    public string BankBin { get; set; } = string.Empty;

    /// <summary>Khi khách tạo QR cọc, thời gian giữ phòng còn ít nhất bấy nhiêu phút.</summary>
    public int HoldExtensionMinutes { get; set; } = 10;

    /// <summary>Đủ thông tin để gọi VietQR sandbox.</summary>
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(BankAccount) && !string.IsNullOrWhiteSpace(BankCode) &&
        !string.IsNullOrWhiteSpace(UserBankName);

    /// <summary>Đang chạy demo: QR tự dựng, không gọi VietQR, xác nhận bằng nút "Giả lập đã thanh toán".</summary>
    public bool IsDemo => Mode.Trim().ToUpperInvariant() switch
    {
        "DEMO" => true,
        "LIVE" => false,
        _ => !HasCredentials
    };
}
