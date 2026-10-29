// Mapped from hotel_management_final_food_inventory.sql; no duplicate tables.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Quanlychuoikhachsan.Domain.Entities;

[Table("ma_xac_thuc")]
public sealed class MaXacThuc : EntityBase
{
    [Column("tai_khoan_id")]
    public long TaiKhoanId { get; set; }
    [Column("loai") , MaxLength(30)]
    public string Loai { get; set; } = null!;
    [Column("ma_hash") , MaxLength(255)]
    public string MaHash { get; set; } = null!;
    [Column("het_han_luc")]
    public DateTime HetHanLuc { get; set; }
    [Column("da_su_dung")]
    public bool DaSuDung { get; set; }
    [Column("su_dung_luc")]
    public DateTime? SuDungLuc { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("khach_san")]
public sealed class KhachSan : EntityBase
{
    [Column("ten_khach_san") , MaxLength(150)]
    public string TenKhachSan { get; set; } = null!;
    [Column("dia_chi") , MaxLength(255)]
    public string DiaChi { get; set; } = null!;
    [Column("so_dien_thoai") , MaxLength(30)]
    public string? SoDienThoai { get; set; }
    [Column("email") , MaxLength(150)]
    public string? Email { get; set; }
    [Column("website") , MaxLength(255)]
    public string? Website { get; set; }
    [Column("gio_nhan_phong")]
    public TimeOnly? GioNhanPhong { get; set; }
    [Column("gio_tra_phong")]
    public TimeOnly? GioTraPhong { get; set; }
    [Column("mo_ta")]
    public string? MoTa { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[Index(nameof(MaTienNghi), IsUnique = true)]
[Table("tien_nghi")]
public sealed class TienNghi : EntityBase
{
    [Column("ma_tien_nghi") , MaxLength(30)]
    public string MaTienNghi { get; set; } = null!;
    [Column("ten_tien_nghi") , MaxLength(120)]
    public string TenTienNghi { get; set; } = null!;
    [Column("icon") , MaxLength(255)]
    public string? Icon { get; set; }
    [Column("mo_ta") , MaxLength(500)]
    public string? MoTa { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[PrimaryKey(nameof(LoaiPhongId), nameof(TienNghiId))]
[Table("loai_phong_tien_nghi")]
public sealed class LoaiPhongTienNghi
{
    [Column("loai_phong_id")]
    public long LoaiPhongId { get; set; }
    [Column("tien_nghi_id")]
    public long TienNghiId { get; set; }
}

[Table("hinh_anh_loai_phong")]
public sealed class HinhAnhLoaiPhong : EntityBase
{
    [Column("loai_phong_id")]
    public long LoaiPhongId { get; set; }
    [Column("url") , MaxLength(1000)]
    public string Url { get; set; } = null!;
    [Column("alt_text") , MaxLength(255)]
    public string? AltText { get; set; }
    [Column("la_anh_dai_dien")]
    public bool LaAnhDaiDien { get; set; }
    [Column("thu_tu")]
    public int ThuTu { get; set; }
}

[Table("hinh_anh_phong")]
public sealed class HinhAnhPhong : EntityBase
{
    [Column("phong_id")]
    public long PhongId { get; set; }
    [Column("url") , MaxLength(1000)]
    public string Url { get; set; } = null!;
    [Column("alt_text") , MaxLength(255)]
    public string? AltText { get; set; }
    [Column("la_anh_dai_dien")]
    public bool LaAnhDaiDien { get; set; }
    [Column("thu_tu")]
    public int ThuTu { get; set; }
}

[Table("chinh_sach_coc")]
public sealed class ChinhSachCoc : EntityBase
{
    [Column("ten_chinh_sach") , MaxLength(150)]
    public string TenChinhSach { get; set; } = null!;
    [Column("kieu_tinh") , MaxLength(30)]
    public string KieuTinh { get; set; } = null!;
    [Column("gia_tri")]
    [Precision(15,2)]
    public decimal GiaTri { get; set; }
    [Column("tu_ngay")]
    public DateOnly? TuNgay { get; set; }
    [Column("den_ngay")]
    public DateOnly? DenNgay { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[Table("chinh_sach_huy")]
public sealed class ChinhSachHuy : EntityBase
{
    [Column("ten_chinh_sach") , MaxLength(150)]
    public string TenChinhSach { get; set; } = null!;
    [Column("mo_ta") , MaxLength(1000)]
    public string? MoTa { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[Table("chi_tiet_chinh_sach_huy")]
public sealed class ChiTietChinhSachHuy : EntityBase
{
    [Column("chinh_sach_huy_id")]
    public long ChinhSachHuyId { get; set; }
    [Column("so_gio_truoc_toi_thieu")]
    public int SoGioTruocToiThieu { get; set; }
    [Column("phan_tram_hoan")]
    [Precision(5,2)]
    public decimal PhanTramHoan { get; set; }
    [Column("phi_huy_co_dinh")]
    [Precision(15,2)]
    public decimal PhiHuyCoDinh { get; set; }
}

[Table("chinh_sach_phu_thu")]
public sealed class ChinhSachPhuThu : EntityBase
{
    [Column("loai") , MaxLength(30)]
    public string Loai { get; set; } = null!;
    [Column("ten_chinh_sach") , MaxLength(150)]
    public string TenChinhSach { get; set; } = null!;
    [Column("tu_phut")]
    public int TuPhut { get; set; }
    [Column("den_phut")]
    public int? DenPhut { get; set; }
    [Column("kieu_tinh") , MaxLength(30)]
    public string KieuTinh { get; set; } = null!;
    [Column("gia_tri")]
    [Precision(15,2)]
    public decimal GiaTri { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[Table("lich_su_trang_thai_dat_phong")]
public sealed class LichSuTrangThaiDatPhong : EntityBase
{
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("trang_thai_cu") , MaxLength(30)]
    public string? TrangThaiCu { get; set; }
    [Column("trang_thai_moi") , MaxLength(30)]
    public string TrangThaiMoi { get; set; } = null!;
    [Column("tai_khoan_thuc_hien_id")]
    public long? TaiKhoanThucHienId { get; set; }
    [Column("ly_do") , MaxLength(1000)]
    public string? LyDo { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("yeu_cau_thay_doi_dat_phong")]
public sealed class YeuCauThayDoiDatPhong : EntityBase
{
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("khach_hang_id")]
    public long KhachHangId { get; set; }
    [Column("loai_yeu_cau") , MaxLength(30)]
    public string LoaiYeuCau { get; set; } = null!;
    [Column("noi_dung") , MaxLength(1500)]
    public string NoiDung { get; set; } = null!;
    [Column("du_kien_nhan_moi")]
    public DateTime? DuKienNhanMoi { get; set; }
    [Column("du_kien_tra_moi")]
    public DateTime? DuKienTraMoi { get; set; }
    [Column("phong_moi_id")]
    public long? PhongMoiId { get; set; }
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "CHO_XU_LY";
    [Column("nhan_vien_xu_ly_id")]
    public long? NhanVienXuLyId { get; set; }
    [Column("ket_qua") , MaxLength(1500)]
    public string? KetQua { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("xu_ly_luc")]
    public DateTime? XuLyLuc { get; set; }
}

[Table("lich_su_doi_phong")]
public sealed class LichSuDoiPhong : EntityBase
{
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("chi_tiet_dat_phong_id")]
    public long ChiTietDatPhongId { get; set; }
    [Column("phong_cu_id")]
    public long PhongCuId { get; set; }
    [Column("phong_moi_id")]
    public long PhongMoiId { get; set; }
    [Column("nhan_vien_id")]
    public long NhanVienId { get; set; }
    [Column("ly_do") , MaxLength(1000)]
    public string? LyDo { get; set; }
    [Column("doi_luc")]
    public DateTime DoiLuc { get; set; } = DateTime.UtcNow;
}

[Table("khach_luu_tru")]
public sealed class KhachLuuTru : EntityBase
{
    [Column("chi_tiet_dat_phong_id")]
    public long ChiTietDatPhongId { get; set; }
    [Column("ho_ten") , MaxLength(120)]
    public string HoTen { get; set; } = null!;
    [Column("ngay_sinh")]
    public DateOnly? NgaySinh { get; set; }
    [Column("gioi_tinh") , MaxLength(20)]
    public string? GioiTinh { get; set; }
    [Column("loai_giay_to") , MaxLength(30)]
    public string? LoaiGiayTo { get; set; }
    [Column("so_giay_to") , MaxLength(50)]
    public string? SoGiayTo { get; set; }
    [Column("quoc_tich") , MaxLength(80)]
    public string? QuocTich { get; set; }
    [Column("la_khach_dai_dien")]
    public bool LaKhachDaiDien { get; set; }
}

[Index(nameof(MaDichVu), IsUnique = true)]
[Table("dich_vu")]
public sealed class DichVu : EntityBase
{
    [Column("ma_dich_vu") , MaxLength(30)]
    public string MaDichVu { get; set; } = null!;
    [Column("ten_dich_vu") , MaxLength(120)]
    public string TenDichVu { get; set; } = null!;
    [Column("nhom_dich_vu") , MaxLength(80)]
    public string? NhomDichVu { get; set; }
    [Column("don_vi_tinh") , MaxLength(30)]
    public string DonViTinh { get; set; } = null!;
    [Column("don_gia")]
    [Precision(15,2)]
    public decimal DonGia { get; set; }
    [Column("mo_ta") , MaxLength(1000)]
    public string? MoTa { get; set; }
    [Column("trang_thai") , MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
}

[Index(nameof(MaSuDung), IsUnique = true)]
[Table("su_dung_dich_vu")]
public sealed class SuDungDichVu : EntityBase
{
    [Column("ma_su_dung") , MaxLength(40)]
    public string MaSuDung { get; set; } = null!;
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("phong_id")]
    public long? PhongId { get; set; }
    [Column("dich_vu_id")]
    public long DichVuId { get; set; }
    [Column("tai_khoan_yeu_cau_id")]
    public long? TaiKhoanYeuCauId { get; set; }
    [Column("thoi_gian_yeu_cau")]
    public DateTime ThoiGianYeuCau { get; set; } = DateTime.UtcNow;
    [Column("thoi_gian_phuc_vu")]
    public DateTime? ThoiGianPhucVu { get; set; }
    [Column("so_luong")]
    [Precision(12,2)]
    public decimal SoLuong { get; set; }
    [Column("don_gia")]
    [Precision(15,2)]
    public decimal DonGia { get; set; }
    [Column("thanh_tien")]
    [Precision(15,2)]
    public decimal ThanhTien { get; set; }
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "CHO_XU_LY";
    [Column("ghi_chu") , MaxLength(500)]
    public string? GhiChu { get; set; }
}

[Index(nameof(MaHoaDon), IsUnique = true)]
[Table("hoa_don_dich_vu")]
public sealed class HoaDonDichVu : EntityBase
{
    [Column("ma_hoa_don") , MaxLength(40)]
    public string MaHoaDon { get; set; } = null!;
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("nhan_vien_lap_id")]
    public long? NhanVienLapId { get; set; }
    [Column("ngay_lap")]
    public DateTime NgayLap { get; set; } = DateTime.UtcNow;
    [Column("tong_tien")]
    [Precision(15,2)]
    public decimal TongTien { get; set; }
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "NHAP";
    [Column("so_hoa_don_dien_tu") , MaxLength(100)]
    public string? SoHoaDonDienTu { get; set; }
    [Column("url_hoa_don_dien_tu") , MaxLength(1000)]
    public string? UrlHoaDonDienTu { get; set; }
}

[Index(nameof(SuDungDichVuId), IsUnique = true)]
[Table("chi_tiet_hoa_don_dich_vu")]
public sealed class ChiTietHoaDonDichVu : EntityBase
{
    [Column("hoa_don_dich_vu_id")]
    public long HoaDonDichVuId { get; set; }
    [Column("su_dung_dich_vu_id")]
    public long SuDungDichVuId { get; set; }
    [Column("thanh_tien")]
    [Precision(15,2)]
    public decimal ThanhTien { get; set; }
}

[Index(nameof(MaThanhToan), IsUnique = true)]
[Table("thanh_toan_dich_vu")]
public sealed class ThanhToanDichVu : EntityBase
{
    [Column("ma_thanh_toan") , MaxLength(40)]
    public string MaThanhToan { get; set; } = null!;
    [Column("hoa_don_dich_vu_id")]
    public long HoaDonDichVuId { get; set; }
    [Column("nhan_vien_thu_id")]
    public long? NhanVienThuId { get; set; }
    [Column("phuong_thuc_id")]
    public long PhuongThucId { get; set; }
    [Column("ca_lam_viec_id")]
    public long? CaLamViecId { get; set; }
    [Column("so_tien")]
    [Precision(15,2)]
    public decimal SoTien { get; set; }
    [Column("thoi_gian_thanh_toan")]
    public DateTime ThoiGianThanhToan { get; set; } = DateTime.UtcNow;
    [Column("ma_giao_dich") , MaxLength(150)]
    public string? MaGiaoDich { get; set; }
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "THANH_CONG";
}

[Index(nameof(MaPhieuHoan), IsUnique = true)]
[Table("phieu_hoan_tien")]
public sealed class PhieuHoanTien : EntityBase
{
    [Column("ma_phieu_hoan") , MaxLength(40)]
    public string MaPhieuHoan { get; set; } = null!;
    [Column("phieu_coc_id")]
    public long? PhieuCocId { get; set; }
    [Column("thanh_toan_hdp_id")]
    public long? ThanhToanHdpId { get; set; }
    [Column("thanh_toan_dv_id")]
    public long? ThanhToanDvId { get; set; }
    [Column("yeu_cau_huy_id")]
    public long? YeuCauHuyId { get; set; }
    [Column("nhan_vien_id")]
    public long? NhanVienId { get; set; }
    [Column("phuong_thuc_id")]
    public long PhuongThucId { get; set; }
    [Column("so_tien")]
    [Precision(15,2)]
    public decimal SoTien { get; set; }
    [Column("ly_do") , MaxLength(1000)]
    public string LyDo { get; set; } = null!;
    [Column("thoi_gian_hoan")]
    public DateTime ThoiGianHoan { get; set; } = DateTime.UtcNow;
    [Column("ma_giao_dich") , MaxLength(150)]
    public string? MaGiaoDich { get; set; }
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "CHO_XU_LY";
}

[Index(nameof(CaLamViecId), nameof(PhuongThucId), IsUnique = true)]
[Table("doi_soat_ca")]
public sealed class DoiSoatCa : EntityBase
{
    [Column("ca_lam_viec_id")]
    public long CaLamViecId { get; set; }
    [Column("phuong_thuc_id")]
    public long PhuongThucId { get; set; }
    [Column("tong_he_thong")]
    [Precision(15,2)]
    public decimal TongHeThong { get; set; }
    [Column("tong_thuc_te")]
    [Precision(15,2)]
    public decimal TongThucTe { get; set; }
    [Column("chenh_lech")]
    [Precision(15,2)]
    public decimal ChenhLech { get; set; }
    [Column("ghi_chu") , MaxLength(500)]
    public string? GhiChu { get; set; }
    [Column("doi_soat_luc")]
    public DateTime DoiSoatLuc { get; set; } = DateTime.UtcNow;
}

[Table("thong_bao")]
public sealed class ThongBao : EntityBase
{
    [Column("loai_thong_bao") , MaxLength(50)]
    public string LoaiThongBao { get; set; } = null!;
    [Column("tieu_de") , MaxLength(255)]
    public string TieuDe { get; set; } = null!;
    [Column("noi_dung") , MaxLength(3000)]
    public string NoiDung { get; set; } = null!;
    [Column("dat_phong_id")]
    public long? DatPhongId { get; set; }
    [Column("check_in_luc")]
    public DateTime? CheckInLuc { get; set; }
    [Column("check_out_luc")]
    public DateTime? CheckOutLuc { get; set; }
    [Column("muc_do") , MaxLength(20)]
    public string MucDo { get; set; } = "THONG_THUONG";
    [Column("duong_dan") , MaxLength(1000)]
    public string? DuongDan { get; set; }
    [Column("tai_khoan_tao_id")]
    public long? TaiKhoanTaoId { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[PrimaryKey(nameof(ThongBaoId), nameof(TaiKhoanId), nameof(KenhGui))]
[Table("thong_bao_tai_khoan")]
public sealed class ThongBaoTaiKhoan
{
    [Column("thong_bao_id")]
    public long ThongBaoId { get; set; }
    [Column("tai_khoan_id")]
    public long TaiKhoanId { get; set; }
    [Column("kenh_gui") , MaxLength(30)]
    public string KenhGui { get; set; } = "APP";
    [Column("da_doc")]
    public bool DaDoc { get; set; }
    [Column("da_doc_luc")]
    public DateTime? DaDocLuc { get; set; }
    [Column("trang_thai_gui") , MaxLength(30)]
    public string TrangThaiGui { get; set; } = "CHO_GUI";
    [Column("gui_luc")]
    public DateTime? GuiLuc { get; set; }
}

[Table("cai_dat_thong_bao")]
[PrimaryKey(nameof(TaiKhoanId))]
public sealed class CaiDatThongBao
{
    [Column("tai_khoan_id")]
    public long TaiKhoanId { get; set; }
    [Column("nhan_booking")]
    public bool NhanBooking { get; set; } = true;
    [Column("nhan_thanh_toan")]
    public bool NhanThanhToan { get; set; } = true;
    [Column("nhan_van_hanh")]
    public bool NhanVanHanh { get; set; } = true;
    [Column("cho_phep_email")]
    public bool ChoPhepEmail { get; set; } = true;
    [Column("cho_phep_push")]
    public bool ChoPhepPush { get; set; } = true;
}

[Index(nameof(MaDanhGia), IsUnique = true)]
[Index(nameof(DatPhongId), IsUnique = true)]
[Table("danh_gia")]
public sealed class DanhGia : EntityBase
{
    [Column("ma_danh_gia") , MaxLength(40)]
    public string MaDanhGia { get; set; } = null!;
    [Column("dat_phong_id")]
    public long DatPhongId { get; set; }
    [Column("khach_hang_id")]
    public long KhachHangId { get; set; }
    [Column("diem_tong")]
    public byte DiemTong { get; set; }
    [Column("diem_phong")]
    public byte? DiemPhong { get; set; }
    [Column("diem_dich_vu")]
    public byte? DiemDichVu { get; set; }
    [Column("noi_dung") , MaxLength(3000)]
    public string? NoiDung { get; set; }
    [Column("ngay_danh_gia")]
    public DateTime NgayDanhGia { get; set; } = DateTime.UtcNow;
    [Column("trang_thai") , MaxLength(30)]
    public string TrangThai { get; set; } = "HIEN_THI";
    [Column("phan_hoi_khach_san") , MaxLength(3000)]
    public string? PhanHoiKhachSan { get; set; }
    [Column("nhan_vien_phan_hoi_id")]
    public long? NhanVienPhanHoiId { get; set; }
    [Column("ngay_phan_hoi")]
    public DateTime? NgayPhanHoi { get; set; }
}
