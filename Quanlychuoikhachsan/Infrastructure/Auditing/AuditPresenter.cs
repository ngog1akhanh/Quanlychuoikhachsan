using System.Globalization;
using System.Text.Json;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Infrastructure.Auditing;

/// <summary>Một dòng thay đổi trong nhật ký: trường, giá trị cũ → mới (đã định dạng để đọc).</summary>
public sealed record AuditChange(string Field, string Label, string? Old, string? New);

/// <summary>
/// Nhật ký hệ thống ở dạng dễ đọc cho App quản lý. Giữ nguyên các trường gốc (tai_khoan_id, hanh_dong, doi_tuong,
/// doi_tuong_id, du_lieu_cu, du_lieu_moi…) để client cũ vẫn chạy, thêm tên người làm, nhãn tiếng Việt, tóm tắt và danh sách thay đổi.
/// </summary>
public sealed record AuditLogView(
    long Id, DateTime CreatedAt, long? TaiKhoanId, string AccountName,
    string HanhDong, string ActionLabel, string? DoiTuong, string ObjectLabel,
    string? DoiTuongId, string? RecordCode, string Summary, IReadOnlyList<AuditChange> Changes,
    string? DuLieuCu, string? DuLieuMoi, string? DiaChiIp, string? UserAgent);

/// <summary>Diễn giải bản ghi nhat_ky_he_thong: dịch tên bảng, hành động, tên trường, trạng thái; định dạng ngày giờ và tiền.</summary>
public static class AuditPresenter
{
    /// <summary>
    /// Bảng kỹ thuật sinh ra hàng loạt theo nghiệp vụ chính (khóa lịch từng đêm, giá từng đêm, thông báo, mã OTP).
    /// Không ghi nhật ký và mặc định ẩn khỏi danh sách – thao tác gốc (đặt phòng, đổi ngày…) đã được ghi ở bảng chính.
    /// </summary>
    public static readonly HashSet<string> SystemTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "khoa_lich_phong", "gia_dem_dat_phong", "thong_bao", "thong_bao_tai_khoan", "cai_dat_thong_bao", "ma_xac_thuc"
    };

    /// <summary>Trường chỉ là dấu thời gian kỹ thuật – không coi là "thay đổi" và không đưa vào tóm tắt.</summary>
    public static readonly HashSet<string> NoiseFields = new(StringComparer.OrdinalIgnoreCase) { "created_at", "updated_at" };

    private static readonly Dictionary<string, string> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dat_phong"] = "Đặt phòng", ["chi_tiet_dat_phong"] = "Phòng trong booking", ["yeu_cau_huy_dat_phong"] = "Yêu cầu hủy",
        ["yeu_cau_thay_doi_dat_phong"] = "Yêu cầu đổi booking", ["lich_su_trang_thai_dat_phong"] = "Lịch sử trạng thái booking",
        ["lich_su_doi_phong"] = "Lịch sử đổi phòng", ["khach_luu_tru"] = "Khách lưu trú",
        ["hoa_don_phong"] = "Hóa đơn phòng", ["chi_tiet_hoa_don_phong"] = "Dòng hóa đơn phòng", ["thanh_toan_hoa_don_phong"] = "Thu tiền hóa đơn phòng",
        ["ap_dung_coc_hoa_don"] = "Trừ cọc vào hóa đơn", ["phieu_coc"] = "Phiếu cọc", ["phieu_hoan_tien"] = "Phiếu hoàn tiền",
        ["hoa_don_dich_vu"] = "Hóa đơn dịch vụ", ["chi_tiet_hoa_don_dich_vu"] = "Dòng hóa đơn dịch vụ", ["thanh_toan_dich_vu"] = "Thu tiền dịch vụ",
        ["ca_lam_viec"] = "Ca thu ngân", ["doi_soat_ca"] = "Đối soát ca", ["phuong_thuc_thanh_toan"] = "Phương thức thanh toán",
        ["vietqr_deposit_order"] = "Đơn cọc VietQR", ["vietqr_payment_order"] = "Thanh toán VietQR",
        ["phong"] = "Phòng", ["loai_phong"] = "Loại phòng", ["tien_nghi"] = "Tiện nghi", ["loai_phong_tien_nghi"] = "Tiện nghi loại phòng",
        ["hinh_anh_phong"] = "Ảnh phòng", ["hinh_anh_loai_phong"] = "Ảnh loại phòng", ["khach_san"] = "Thông tin khách sạn", ["noi_dung_web"] = "Nội dung website",
        ["chinh_sach_gia"] = "Chính sách giá", ["chi_tiet_chinh_sach_gia"] = "Chi tiết chính sách giá", ["chinh_sach_gia_thu"] = "Thứ áp dụng giá",
        ["chinh_sach_coc"] = "Chính sách cọc", ["chinh_sach_huy"] = "Chính sách hủy", ["chi_tiet_chinh_sach_huy"] = "Mốc chính sách hủy",
        ["chinh_sach_phu_thu"] = "Phụ thu sớm/trễ",
        ["dich_vu"] = "Dịch vụ", ["su_dung_dich_vu"] = "Yêu cầu dịch vụ",
        ["cong_viec_buong_phong"] = "Công việc buồng phòng", ["tinh_trang_phong"] = "Sự cố & hư hỏng",
        ["khach_hang"] = "Khách hàng", ["danh_gia"] = "Đánh giá",
        ["tai_khoan"] = "Tài khoản", ["nhan_vien"] = "Nhân viên", ["chuc_vu"] = "Chức vụ", ["vai_tro"] = "Vai trò", ["quyen"] = "Quyền",
        ["tai_khoan_vai_tro"] = "Gán vai trò", ["vai_tro_quyen"] = "Phân quyền vai trò",
        ["kho"] = "Kho", ["nha_cung_cap"] = "Nhà cung cấp", ["nhom_hang"] = "Nhóm hàng", ["don_vi_tinh"] = "Đơn vị tính",
        ["danh_muc_hang_hoa"] = "Mặt hàng", ["phieu_nhap"] = "Phiếu nhập", ["chi_tiet_phieu_nhap"] = "Dòng phiếu nhập",
        ["phieu_xuat"] = "Phiếu xuất", ["chi_tiet_phieu_xuat"] = "Dòng phiếu xuất", ["lo_hang"] = "Lô hàng",
        ["giao_dich_kho"] = "Sổ kho", ["tai_san_ca_the"] = "Tài sản",
        ["khoa_lich_phong"] = "Khóa lịch phòng", ["gia_dem_dat_phong"] = "Giá từng đêm", ["thong_bao"] = "Thông báo",
        ["thong_bao_tai_khoan"] = "Hộp thư thông báo", ["cai_dat_thong_bao"] = "Cài đặt thông báo", ["ma_xac_thuc"] = "Mã xác thực",
    };

    private static readonly Dictionary<string, string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CREATE"] = "Thêm mới", ["UPDATE"] = "Cập nhật", ["DELETE"] = "Xóa", ["VOID"] = "Hủy chứng từ",
        ["LOGIN"] = "Đăng nhập", ["LOGOUT"] = "Đăng xuất",
    };

    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "ID", ["trang_thai"] = "Trạng thái", ["trang_thai_xu_ly"] = "Trạng thái xử lý", ["ghi_chu"] = "Ghi chú", ["ly_do"] = "Lý do",
        ["ly_do_huy"] = "Lý do hủy", ["reason"] = "Lý do", ["mo_ta"] = "Mô tả",
        ["ho_ten"] = "Họ tên", ["email"] = "Email", ["so_dien_thoai"] = "Số điện thoại", ["dia_chi"] = "Địa chỉ", ["so_giay_to"] = "Số giấy tờ",
        ["contact_name"] = "Tên người đặt", ["contact_phone"] = "SĐT người đặt", ["contact_email"] = "Email người đặt",
        ["ma_dat_phong"] = "Mã booking", ["ngay_dat"] = "Ngày đặt", ["du_kien_nhan"] = "Nhận phòng dự kiến", ["du_kien_tra"] = "Trả phòng dự kiến",
        ["nhan_phong_luc"] = "Nhận phòng lúc", ["tra_phong_luc"] = "Trả phòng lúc", ["so_nguoi_lon"] = "Người lớn", ["so_tre_em"] = "Trẻ em",
        ["kenh_dat"] = "Kênh đặt", ["yeu_cau_dac_biet"] = "Yêu cầu đặc biệt", ["tong_tien_du_kien"] = "Tổng tiền dự kiến", ["tong_tien_phong_goc"] = "Tiền phòng gốc",
        ["tien_phong_du_kien"] = "Tiền phòng dự kiến", ["phong_id"] = "Phòng (ID)", ["dat_phong_id"] = "Booking (ID)", ["khach_hang_id"] = "Khách hàng (ID)",
        ["so_tien_hoan_du_kien"] = "Hoàn dự kiến", ["so_tien_hoan_duyet"] = "Hoàn được duyệt", ["thoi_gian_yeu_cau"] = "Thời gian yêu cầu", ["thoi_gian_xu_ly"] = "Thời gian xử lý",
        ["ma_hoa_don"] = "Mã hóa đơn", ["ngay_lap"] = "Ngày lập", ["tien_phong"] = "Tiền phòng", ["tien_phu_thu"] = "Phụ thu", ["tien_boi_thuong"] = "Bồi thường",
        ["tien_thue"] = "Tiền thuế", ["tong_tien"] = "Tổng tiền", ["tong_tien_hang"] = "Tiền hàng", ["chiet_khau"] = "Chiết khấu", ["tong_thanh_toan"] = "Tổng thanh toán",
        ["so_tien"] = "Số tiền", ["so_tien_ap_dung"] = "Số tiền trừ", ["ma_giao_dich"] = "Mã giao dịch", ["cong_thanh_toan"] = "Cổng thanh toán",
        ["phuong_thuc_id"] = "Phương thức (ID)", ["thoi_gian_thanh_toan"] = "Thời gian thu", ["thoi_gian_coc"] = "Thời gian cọc", ["thoi_gian_hoan"] = "Thời gian hoàn",
        ["ma_phieu_coc"] = "Mã phiếu cọc", ["ma_phieu_hoan"] = "Mã phiếu hoàn", ["ma_thanh_toan"] = "Mã thanh toán",
        ["tien_dau_ca"] = "Tiền đầu ca", ["bat_dau_luc"] = "Bắt đầu", ["ket_thuc_luc"] = "Kết thúc", ["tong_he_thong"] = "Hệ thống", ["tong_thuc_te"] = "Thực tế", ["chenh_lech"] = "Chênh lệch",
        ["so_phong"] = "Số phòng", ["ma_phong"] = "Mã phòng", ["tang"] = "Tầng", ["huong"] = "Hướng", ["loai_view"] = "View", ["phu_thu_dem_mac_dinh"] = "Phụ thu/đêm",
        ["ten_loai_phong"] = "Tên loại phòng", ["ma_loai_phong"] = "Mã loại phòng", ["gia_co_ban_dem"] = "Giá cơ bản/đêm", ["dien_tich"] = "Diện tích",
        ["so_nguoi_lon_toi_da"] = "Người lớn tối đa", ["so_tre_em_toi_da"] = "Trẻ em tối đa", ["loai_phong_id"] = "Loại phòng (ID)",
        ["ma_chinh_sach"] = "Mã chính sách", ["ten_chinh_sach"] = "Tên chính sách", ["tu_ngay"] = "Từ ngày", ["den_ngay"] = "Đến ngày", ["muc_uu_tien"] = "Ưu tiên",
        ["kieu_dieu_chinh"] = "Kiểu điều chỉnh", ["kieu_tinh"] = "Kiểu tính", ["gia_tri"] = "Giá trị", ["phan_tram_hoan"] = "% hoàn", ["so_gio_truoc_toi_thieu"] = "Số giờ trước",
        ["ten_dich_vu"] = "Tên dịch vụ", ["ma_dich_vu"] = "Mã dịch vụ", ["nhom_dich_vu"] = "Nhóm dịch vụ", ["don_vi_tinh"] = "Đơn vị tính", ["don_gia"] = "Đơn giá",
        ["so_luong"] = "Số lượng", ["thanh_tien"] = "Thành tiền", ["thoi_gian_phuc_vu"] = "Thời gian phục vụ",
        ["ma_cong_viec"] = "Mã công việc", ["loai_cong_viec"] = "Loại công việc", ["nhan_vien_duoc_giao_id"] = "Người được giao (ID)", ["muc_do_uu_tien"] = "Mức ưu tiên",
        ["loai_tinh_trang"] = "Loại sự cố", ["muc_do"] = "Mức độ", ["chi_phi_du_kien"] = "Chi phí dự kiến", ["chi_phi_thuc_te"] = "Chi phí thực tế", ["do_khach_gay_ra"] = "Do khách gây ra",
        ["ma_nhan_vien"] = "Mã nhân viên", ["chuc_vu_id"] = "Chức vụ (ID)", ["tai_khoan_id"] = "Tài khoản (ID)", ["vai_tro_id"] = "Vai trò (ID)", ["quyen_id"] = "Quyền (ID)",
        ["ma_vai_tro"] = "Mã vai trò", ["ten_vai_tro"] = "Tên vai trò", ["ma_quyen"] = "Mã quyền", ["loai_tai_khoan"] = "Loại tài khoản", ["lan_dang_nhap_cuoi"] = "Đăng nhập cuối",
        ["ma_khach_hang"] = "Mã khách hàng", ["diem_tong"] = "Điểm", ["noi_dung"] = "Nội dung", ["phan_hoi_khach_san"] = "Phản hồi",
        ["ma_hang"] = "Mã hàng", ["ten_hang"] = "Tên hàng", ["loai_hang"] = "Loại hàng", ["gia_von_tham_chieu"] = "Giá vốn tham chiếu", ["muc_ton_toi_thieu"] = "Tồn tối thiểu",
        ["ma_phieu_nhap"] = "Mã phiếu nhập", ["ma_phieu_xuat"] = "Mã phiếu xuất", ["nha_cung_cap_id"] = "Nhà cung cấp (ID)", ["kho_id"] = "Kho (ID)",
        ["ngay_nhap"] = "Ngày nhập", ["ngay_xuat"] = "Ngày xuất", ["xac_nhan_luc"] = "Xác nhận lúc", ["so_hoa_don_ncc"] = "Số HĐ NCC", ["don_gia_nhap"] = "Đơn giá nhập",
        ["muc_dich"] = "Mục đích", ["nguoi_nhan"] = "Người nhận", ["so_luong_thay_doi"] = "SL thay đổi", ["loai_giao_dich"] = "Loại giao dịch", ["ma_lo"] = "Mã lô",
        ["han_su_dung"] = "Hạn sử dụng", ["ma_tai_san"] = "Mã tài sản", ["so_serial"] = "Serial", ["gia_mua"] = "Giá mua",
        ["ten_nha_cung_cap"] = "Tên nhà cung cấp", ["ma_nha_cung_cap"] = "Mã NCC", ["ten_kho"] = "Tên kho", ["ma_kho"] = "Mã kho",
        ["created_at"] = "Tạo lúc", ["updated_at"] = "Cập nhật lúc",
    };

    private static readonly Dictionary<string, string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HOLD"] = "Giữ chỗ", ["CHO_COC"] = "Chờ cọc", ["BOOKED"] = "Đã xác nhận", ["DANG_O"] = "Đang ở", ["DA_TRA"] = "Đã trả phòng", ["DA_HUY"] = "Đã hủy",
        ["CHO_DUYET_HUY"] = "Chờ duyệt hủy", ["NO_SHOW"] = "Khách không đến", ["HET_HAN"] = "Hết hạn",
        ["NHAP"] = "Nháp", ["DA_PHAT_HANH"] = "Đã phát hành", ["TT_MOT_PHAN"] = "Thanh toán một phần", ["DA_THANH_TOAN"] = "Đã thanh toán",
        ["CHO_XU_LY"] = "Chờ xử lý", ["DANG_XU_LY"] = "Đang xử lý", ["HOAN_THANH"] = "Hoàn thành", ["THANH_CONG"] = "Thành công", ["THAT_BAI"] = "Thất bại",
        ["CHO_DUYET"] = "Chờ duyệt", ["DA_DUYET"] = "Đã duyệt", ["TU_CHOI"] = "Từ chối", ["DA_XAC_NHAN"] = "Đã xác nhận",
        ["HOAT_DONG"] = "Hoạt động", ["NGUNG_HOAT_DONG"] = "Ngừng hoạt động", ["KHOA"] = "Đã khóa", ["DANG_LAM"] = "Đang làm", ["NGHI_VIEC"] = "Nghỉ việc",
        ["SAN_SANG"] = "Sẵn sàng", ["CAN_DON"] = "Cần dọn", ["DANG_DON"] = "Đang dọn", ["BAO_TRI"] = "Bảo trì", ["NGUNG_SU_DUNG"] = "Ngừng sử dụng",
        ["DANG_MO"] = "Đang mở", ["DA_DONG"] = "Đã đóng", ["PENDING"] = "Chờ thanh toán", ["PAID"] = "Đã thanh toán", ["REFUNDED"] = "Đã hoàn",
    };

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string TableLabel(string? table) => table is null ? "" : Tables.GetValueOrDefault(table, table);

    /// <summary>Tên bảng khớp với từ khóa tìm kiếm (theo tên bảng hoặc nhãn tiếng Việt).</summary>
    public static List<string> TablesMatching(string search)
        => Tables.Where(t => t.Key.Contains(search, StringComparison.OrdinalIgnoreCase) || t.Value.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Key).Append(search).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static AuditLogView Describe(NhatKyHeThong log, string accountName)
    {
        var oldValues = Parse(log.DuLieuCu);
        var newValues = Parse(log.DuLieuMoi);
        var changes = BuildChanges(log.HanhDong, oldValues, newValues);
        var code = FindCode(newValues) ?? FindCode(oldValues);
        // Bản ghi cũ (trước khi sửa interceptor) lưu khóa tạm âm của EF khi thêm mới → coi như không có ID.
        var recordId = long.TryParse(log.DoiTuongId, out var parsed) && parsed <= 0 ? null : log.DoiTuongId;
        return new AuditLogView(log.Id, log.CreatedAt, log.TaiKhoanId, accountName,
            log.HanhDong, Actions.GetValueOrDefault(log.HanhDong, log.HanhDong), log.DoiTuong, TableLabel(log.DoiTuong),
            recordId, code, Summary(log.HanhDong, changes, newValues), changes,
            log.DuLieuCu, log.DuLieuMoi, log.DiaChiIp, log.UserAgent);
    }

    private static Dictionary<string, JsonElement> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new(StringComparer.OrdinalIgnoreCase);
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase) { ["noi_dung"] = JsonDocument.Parse(JsonSerializer.Serialize(json)).RootElement.Clone() };
        }
    }

    private static List<AuditChange> BuildChanges(string action, Dictionary<string, JsonElement> oldValues, Dictionary<string, JsonElement> newValues)
    {
        var keys = oldValues.Keys.Concat(newValues.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !NoiseFields.Contains(k) && !(k.Equals("id", StringComparison.OrdinalIgnoreCase) && action is "CREATE" or "DELETE"));
        var result = new List<AuditChange>();
        foreach (var key in keys)
        {
            var old = oldValues.TryGetValue(key, out var o) ? Format(key, o) : null;
            var now = newValues.TryGetValue(key, out var n) ? Format(key, n) : null;
            if (action == "UPDATE" && old == now) continue;
            result.Add(new AuditChange(key, FieldLabel(key), old, now));
        }
        return result;
    }

    private static string Summary(string action, List<AuditChange> changes, Dictionary<string, JsonElement> newValues)
    {
        if (changes.Count == 0) return action == "UPDATE" ? "Không có thay đổi nội dung" : "";
        if (action == "UPDATE")
            return string.Join("; ", changes.Take(4).Select(c => $"{c.Label}: {c.Old ?? "(trống)"} → {c.New ?? "(trống)"}"))
                + (changes.Count > 4 ? $"; … (+{changes.Count - 4} trường)" : "");
        // Thêm / xóa / thao tác riêng: ưu tiên các trường nhận diện (mã, tên, tiền, trạng thái, lý do).
        var important = changes.OrderBy(c => Priority(c.Field)).Take(4)
            .Select(c => $"{c.Label}: {(action == "DELETE" ? c.Old : c.New) ?? c.Old ?? "(trống)"}");
        return string.Join("; ", important);
    }

    private static int Priority(string field) => field switch
    {
        _ when field.StartsWith("ma_", StringComparison.OrdinalIgnoreCase) => 0,
        _ when field.StartsWith("ten_", StringComparison.OrdinalIgnoreCase) || field is "ho_ten" or "so_phong" => 1,
        "reason" or "ly_do" or "ly_do_huy" => 2,
        _ when field.Contains("tien", StringComparison.OrdinalIgnoreCase) || field is "so_luong" or "don_gia" or "gia_tri" => 3,
        _ when field.StartsWith("trang_thai", StringComparison.OrdinalIgnoreCase) => 4,
        _ when field.EndsWith("_id", StringComparison.OrdinalIgnoreCase) => 9,
        _ => 5
    };

    private static string? FindCode(Dictionary<string, JsonElement> values)
        => values.Where(v => v.Key.StartsWith("ma_", StringComparison.OrdinalIgnoreCase) && v.Value.ValueKind == JsonValueKind.String)
            .Select(v => v.Value.GetString()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    public static string FieldLabel(string field)
    {
        if (Fields.TryGetValue(field, out var label)) return label;
        var text = field.Replace('_', ' ');
        return text.Length == 0 ? field : char.ToUpper(text[0], Vi) + text[1..];
    }

    private static string? Format(string field, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined: return null;
            case JsonValueKind.True: return "Có";
            case JsonValueKind.False: return "Không";
            case JsonValueKind.Number:
                return value.TryGetDecimal(out var number)
                    ? (field.EndsWith("_id", StringComparison.OrdinalIgnoreCase) || field.Equals("id", StringComparison.OrdinalIgnoreCase)
                        ? number.ToString(CultureInfo.InvariantCulture) : number.ToString("#,0.###", Vi))
                    : value.GetRawText();
            case JsonValueKind.String:
                var text = value.GetString() ?? "";
                if (field.StartsWith("trang_thai", StringComparison.OrdinalIgnoreCase) && Statuses.TryGetValue(text, out var status)) return status;
                if (text.Length >= 19 && text[10] == 'T' && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
                    // Database lưu thời điểm theo UTC → hiển thị giờ khách sạn.
                    return HotelTime.Local(time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time).ToString("dd/MM/yyyy HH:mm", Vi);
                if (text.Length == 10 && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    return day.ToString("dd/MM/yyyy", Vi);
                return text.Length > 300 ? text[..300] + "…" : text;
            default:
                var raw = value.GetRawText();
                return raw.Length > 300 ? raw[..300] + "…" : raw;
        }
    }
}
