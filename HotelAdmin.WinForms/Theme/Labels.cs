namespace HotelAdmin.Theme;

/// <summary>Nhãn tiếng Việt và màu pill cho các mã trạng thái/loại dùng ở các module quản lý.</summary>
public static class Labels
{
    private enum Tone { Success, Warning, Danger, Info, Neutral, Lime }

    private static readonly Dictionary<string, (string Label, Tone Tone)> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Chung
        ["HOAT_DONG"] = ("Hoạt động", Tone.Success),
        ["NGUNG_HOAT_DONG"] = ("Ngừng", Tone.Neutral),
        ["KHOA"] = ("Đã khóa", Tone.Danger),
        // Hóa đơn / thanh toán
        ["NHAP"] = ("Nháp", Tone.Neutral),
        ["DA_PHAT_HANH"] = ("Đã phát hành", Tone.Info),
        ["TT_MOT_PHAN"] = ("Thu một phần", Tone.Warning),
        ["DA_THANH_TOAN"] = ("Đã thanh toán", Tone.Success),
        ["DA_HUY"] = ("Đã hủy", Tone.Danger),
        ["THANH_CONG"] = ("Thành công", Tone.Success),
        ["CHO_XU_LY"] = ("Chờ xử lý", Tone.Warning),
        ["DANG_XU_LY"] = ("Đang xử lý", Tone.Info),
        ["DA_XU_LY"] = ("Đã xử lý", Tone.Success),
        ["HOAN_THANH"] = ("Hoàn thành", Tone.Success),
        ["DANG_MO"] = ("Đang mở", Tone.Lime),
        ["DA_DONG"] = ("Đã đóng", Tone.Neutral),
        ["PENDING"] = ("Chờ thanh toán", Tone.Warning),
        ["PAID"] = ("Đã nhận tiền", Tone.Success),
        ["FAILED"] = ("Thất bại", Tone.Danger),
        ["REFUND_REQUIRED"] = ("Cần hoàn tiền", Tone.Danger),
        ["REFUNDED"] = ("Đã hoàn", Tone.Neutral),
        // Buồng phòng
        ["DANG_THUC_HIEN"] = ("Đang làm", Tone.Info),
        ["DON_CHECKOUT"] = ("Dọn sau trả phòng", Tone.Neutral),
        ["DON_HANG_NGAY"] = ("Dọn hằng ngày", Tone.Neutral),
        ["KIEM_TRA"] = ("Kiểm tra phòng", Tone.Neutral),
        ["THAP"] = ("Thấp", Tone.Neutral),
        ["BINH_THUONG"] = ("Bình thường", Tone.Info),
        ["CAO"] = ("Cao", Tone.Warning),
        ["KHAN_CAP"] = ("Khẩn cấp", Tone.Danger),
        ["NHE"] = ("Nhẹ", Tone.Neutral),
        ["TRUNG_BINH"] = ("Trung bình", Tone.Warning),
        ["NGHIEM_TRONG"] = ("Nghiêm trọng", Tone.Danger),
        ["MOI_GHI_NHAN"] = ("Mới ghi nhận", Tone.Warning),
        ["HONG_THIET_BI"] = ("Hỏng thiết bị", Tone.Warning),
        ["MAT_DO"] = ("Mất đồ", Tone.Warning),
        ["VET_BAN"] = ("Vết bẩn khó xử lý", Tone.Warning),
        ["HU_HONG_NOI_THAT"] = ("Hư hỏng nội thất", Tone.Warning),
        ["TIEN_PHONG"] = ("Tiền phòng", Tone.Neutral),
        ["PHU_THU"] = ("Phụ thu", Tone.Neutral),
        ["BOI_THUONG"] = ("Bồi thường", Tone.Warning),
        ["THUE"] = ("Thuế", Tone.Neutral),
        // Đánh giá
        ["HIEN_THI"] = ("Đang hiển thị", Tone.Success),
        ["AN"] = ("Đã ẩn", Tone.Neutral),
        // Nhân viên
        ["DANG_LAM"] = ("Đang làm", Tone.Success),
        ["TAM_NGHI"] = ("Tạm nghỉ", Tone.Warning),
        ["NGHI_VIEC"] = ("Nghỉ việc", Tone.Neutral),
        // Kho
        ["DA_XAC_NHAN"] = ("Đã xác nhận", Tone.Success),
        ["CON_HAN"] = ("Còn hạn", Tone.Success),
        ["SAP_HET_HAN"] = ("Sắp hết hạn", Tone.Warning),
        ["HET_HAN"] = ("Hết hạn", Tone.Danger),
        ["TOT"] = ("Tốt", Tone.Success),
        ["HONG"] = ("Hỏng", Tone.Danger),
        ["MAT"] = ("Mất", Tone.Danger),
        ["BAO_TRI"] = ("Bảo trì", Tone.Warning),
        ["THANH_LY"] = ("Thanh lý", Tone.Neutral),
        ["DANG_SU_DUNG"] = ("Đang dùng", Tone.Info),
        ["TRONG_KHO"] = ("Trong kho", Tone.Neutral),
        ["THUC_PHAM"] = ("Thực phẩm", Tone.Neutral),
        ["DO_UONG"] = ("Đồ uống", Tone.Neutral),
        ["HANG_MINIBAR"] = ("Hàng minibar", Tone.Neutral),
        ["VAT_TU"] = ("Vật tư", Tone.Neutral),
        ["DO_VAI"] = ("Đồ vải", Tone.Neutral),
        ["TAI_SAN"] = ("Tài sản", Tone.Neutral),
        ["KHAC"] = ("Khác", Tone.Neutral),
        ["NHAP_KHO"] = ("Nhập kho", Tone.Success),
        ["XUAT_KHO"] = ("Xuất kho", Tone.Info),
        ["DIEU_CHINH"] = ("Điều chỉnh", Tone.Warning),
        ["HONG_MAT"] = ("Hỏng/mất", Tone.Danger),
        // Thông báo (loại / mức độ)
        ["BOOKING"] = ("Đặt phòng", Tone.Info),
        ["BOOKING_ARRIVAL_REMINDER"] = ("Nhắc khách đến", Tone.Info),
        ["BOOKING_DEPARTURE_REMINDER"] = ("Nhắc khách đi", Tone.Info),
        ["BOOKING_OVERDUE"] = ("Quá giờ trả phòng", Tone.Danger),
        ["PAYMENT"] = ("Thanh toán", Tone.Success),
        ["OPERATIONS"] = ("Nội bộ", Tone.Lime),
        ["HOUSEKEEPING"] = ("Buồng phòng", Tone.Warning),
        ["MAINTENANCE"] = ("Sự cố phòng", Tone.Danger),
        ["LOW_STOCK"] = ("Tồn kho thấp", Tone.Warning),
        ["EXPIRY"] = ("Hạn sử dụng", Tone.Warning),
        ["SERVICE_REQUEST"] = ("Yêu cầu dịch vụ", Tone.Info),
        ["SERVICE_CATALOG"] = ("Danh mục dịch vụ", Tone.Info),
        ["QUAN_TRONG"] = ("Quan trọng", Tone.Danger),
        ["THONG_THUONG"] = ("Thường", Tone.Neutral),
        // Chính sách
        ["PHAN_TRAM"] = ("Phần trăm", Tone.Neutral),
        ["DEM_DAU"] = ("Đêm đầu", Tone.Neutral),
        ["SO_TIEN_CO_DINH"] = ("Số tiền cố định", Tone.Neutral),
        ["NHAN_SOM"] = ("Nhận sớm", Tone.Info),
        ["TRA_TRE"] = ("Trả trễ", Tone.Warning),
        ["GIA_CO_DINH"] = ("Giá cố định", Tone.Neutral),
        ["TANG_%"] = ("Tăng %", Tone.Neutral),
        ["GIAM_%"] = ("Giảm %", Tone.Neutral),
        ["TANG_TIEN"] = ("Tăng tiền", Tone.Neutral),
        ["GIAM_TIEN"] = ("Giảm tiền", Tone.Neutral),
    };

    public static string Of(string? code)
    {
        if (code is null) return "";
        if (Map.TryGetValue(code, out var v)) return v.Label;
        var booking = StatusStyles.ForBooking(code).Label;
        if (booking != code) return booking;
        return StatusStyles.ForRoom(code).Label;
    }

    public static StatusStyle Style(string? code)
    {
        if (code is null || !Map.TryGetValue(code, out var v))
        {
            // Mã trạng thái booking/phòng dùng bảng màu sẵn có
            var booking = StatusStyles.ForBooking(code);
            if (booking.Label != (code ?? "-")) return booking;
            var room = StatusStyles.ForRoom(code);
            return room.Label != (code ?? "-") ? room : new StatusStyle(code ?? "-", AppTheme.NeutralBg, AppTheme.NeutralFg);
        }
        return v.Tone switch
        {
            Tone.Success => new StatusStyle(v.Label, AppTheme.SuccessBg, AppTheme.SuccessFg),
            Tone.Warning => new StatusStyle(v.Label, AppTheme.WarningBg, AppTheme.WarningFg),
            Tone.Danger => new StatusStyle(v.Label, AppTheme.DangerBg, AppTheme.DangerFg),
            Tone.Info => new StatusStyle(v.Label, AppTheme.InfoBg, AppTheme.InfoFg),
            Tone.Lime => new StatusStyle(v.Label, AppTheme.Lime, ColorTranslator.FromHtml("#5B6B10")),
            _ => new StatusStyle(v.Label, AppTheme.NeutralBg, AppTheme.NeutralFg)
        };
    }

    /// <summary>Danh sách lựa chọn (nhãn, mã) cho ComboBox.</summary>
    public static (string Text, string Value)[] Options(params string[] codes) => codes.Select(c => (Of(c), c)).ToArray();
}
