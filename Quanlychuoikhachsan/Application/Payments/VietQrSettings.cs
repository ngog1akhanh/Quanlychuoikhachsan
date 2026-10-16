namespace Quanlychuoikhachsan.Application.Payments;

public sealed class VietQrSettings
{
    public const string SectionName = "VietQR";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string UserBankName { get; set; } = string.Empty;
}
