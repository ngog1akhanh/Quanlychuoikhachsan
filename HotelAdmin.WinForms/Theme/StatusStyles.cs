namespace HotelAdmin.Theme;

/// <summary>Nhãn tiếng Việt + màu "pill" cho các mã trạng thái trong database.</summary>
public readonly record struct StatusStyle(string Label, Color Back, Color Fore);

public static class StatusStyles
{
    private static readonly Dictionary<string, StatusStyle> Booking = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HOLD"] = new("Đang giữ chỗ", AppTheme.WarningBg, AppTheme.WarningFg),
        ["CHO_COC"] = new("Chờ đặt cọc", AppTheme.WarningBg, AppTheme.WarningFg),
        ["BOOKED"] = new("Đã xác nhận", AppTheme.InfoBg, AppTheme.InfoFg),
        ["DANG_O"] = new("Đang lưu trú", AppTheme.SuccessBg, AppTheme.SuccessFg),
        ["DA_TRA"] = new("Đã trả phòng", AppTheme.NeutralBg, AppTheme.NeutralFg),
        ["CHO_DUYET_HUY"] = new("Chờ duyệt hủy", AppTheme.WarningBg, AppTheme.WarningFg),
        ["DA_HUY"] = new("Đã hủy", AppTheme.DangerBg, AppTheme.DangerFg),
        ["NO_SHOW"] = new("Không đến", AppTheme.DangerBg, AppTheme.DangerFg),
    };

    private static readonly Dictionary<string, StatusStyle> Room = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SAN_SANG"] = new("Sẵn sàng", AppTheme.Mint, AppTheme.SuccessFg),
        ["DANG_O"] = new("Đang ở", AppTheme.Lime, ColorTranslator.FromHtml("#5B6B10")),
        ["CAN_DON"] = new("Cần dọn", AppTheme.WarningBg, AppTheme.WarningFg),
        ["DANG_DON"] = new("Đang dọn", AppTheme.InfoBg, AppTheme.InfoFg),
        ["BAO_TRI"] = new("Bảo trì", AppTheme.DangerBg, AppTheme.DangerFg),
        ["NGUNG_SU_DUNG"] = new("Ngừng sử dụng", AppTheme.NeutralBg, AppTheme.NeutralFg),
    };

    public static StatusStyle ForBooking(string? code) => Lookup(Booking, code);

    public static StatusStyle ForRoom(string? code) => Lookup(Room, code);

    /// <summary>Màu dùng cho biểu đồ/thanh trạng thái phòng.</summary>
    public static Color RoomChartColor(string code) => code switch
    {
        "DANG_O" => AppTheme.LimeDark,
        "SAN_SANG" => AppTheme.MintDark,
        "CAN_DON" => ColorTranslator.FromHtml("#F5D86B"),
        "DANG_DON" => ColorTranslator.FromHtml("#9DB8F2"),
        "BAO_TRI" => ColorTranslator.FromHtml("#F2A3A3"),
        _ => ColorTranslator.FromHtml("#D9DDD6")
    };

    private static StatusStyle Lookup(Dictionary<string, StatusStyle> map, string? code)
        => code is not null && map.TryGetValue(code, out var s)
            ? s
            : new StatusStyle(code ?? "-", AppTheme.NeutralBg, AppTheme.NeutralFg);
}
