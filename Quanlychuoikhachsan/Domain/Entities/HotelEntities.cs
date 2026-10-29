namespace Quanlychuoikhachsan.Domain.Entities;

public abstract class EntityBase
{
    public long Id { get; set; }
}

public sealed class YeuCauHuyDatPhong : EntityBase
{
    public string MaYeuCau { get; set; } = null!;
    public long DatPhongId { get; set; }
    public long KhachHangId { get; set; }
    public string LyDo { get; set; } = null!;
    public DateTime ThoiGianYeuCau { get; set; }
    public decimal SoTienHoanDuKien { get; set; }
    public string TrangThai { get; set; } = "CHO_DUYET";
    public long? NhanVienXuLyId { get; set; }
    public DateTime? ThoiGianXuLy { get; set; }
    public decimal SoTienHoanDuyet { get; set; }
    public string? GhiChuXuLy { get; set; }
}

public sealed class HoaDonPhong : EntityBase
{
    public string MaHoaDon { get; set; } = null!;
    public long DatPhongId { get; set; }
    public long? HoaDonThayTheId { get; set; }
    public long? NhanVienLapId { get; set; }
    public DateTime NgayLap { get; set; }
    public decimal TienPhong { get; set; }
    public decimal TienPhuThu { get; set; }
    public decimal TienBoiThuong { get; set; }
    public decimal TienThue { get; set; }
    public decimal TongTien { get; set; }
    public string TrangThai { get; set; } = "NHAP";
    public string? LyDoHuy { get; set; }
    public string? SoHoaDonDienTu { get; set; }
    public string? UrlHoaDonDienTu { get; set; }
    public ICollection<ChiTietHoaDonPhong> ChiTiet { get; set; } = [];
}

public sealed class ChiTietHoaDonPhong : EntityBase
{
    public long HoaDonPhongId { get; set; }
    public long? ChiTietDatPhongId { get; set; }
    public long? TinhTrangPhongId { get; set; }
    public string LoaiDong { get; set; } = null!;
    public string NoiDung { get; set; } = null!;
    public DateOnly? NgayApDung { get; set; }
    public decimal SoLuong { get; set; } = 1;
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}

public sealed class ApDungCocHoaDon
{
    public long HoaDonPhongId { get; set; }
    public long PhieuCocId { get; set; }
    public decimal SoTienApDung { get; set; }
}

public sealed class ThanhToanHoaDonPhong : EntityBase
{
    public string MaThanhToan { get; set; } = null!;
    public long HoaDonPhongId { get; set; }
    public long? NhanVienThuId { get; set; }
    public long PhuongThucId { get; set; }
    public long? CaLamViecId { get; set; }
    public decimal SoTien { get; set; }
    public DateTime ThoiGianThanhToan { get; set; }
    public string? CongThanhToan { get; set; }
    public string? MaGiaoDich { get; set; }
    public string TrangThai { get; set; } = "THANH_CONG";
}

/// <summary>Đơn QR VietQR được lưu để theo dõi PENDING và đối soát Inquiry vào hóa đơn.</summary>
public sealed class YeuCauThanhToanVietQr : EntityBase
{
    public string OrderId { get; set; } = null!;
    public long HoaDonPhongId { get; set; }
    public decimal SoTien { get; set; }
    public string NoiDung { get; set; } = null!;
    public string QrCode { get; set; } = null!;
    public string QrDataUrl { get; set; } = null!;
    public string TrangThai { get; set; } = "PENDING";
    public string? MaGiaoDich { get; set; }
    public string? TransactionDateTime { get; set; }
    public string? GhiChu { get; set; }
    public long? TaiKhoanTaoId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class ChinhSachGia : EntityBase
{
    public string MaChinhSach { get; set; } = null!;
    public string TenChinhSach { get; set; } = null!;
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int MucUuTien { get; set; }
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public ICollection<ChinhSachGiaThu> CacThuApDung { get; set; } = [];
    public ICollection<ChiTietChinhSachGia> ChiTiet { get; set; } = [];
}

public sealed class ChinhSachGiaThu
{
    public long ChinhSachGiaId { get; set; }
    public byte Thu { get; set; }
    public ChinhSachGia ChinhSachGia { get; set; } = null!;
}

public sealed class ChiTietChinhSachGia : EntityBase
{
    public long ChinhSachGiaId { get; set; }
    public long LoaiPhongId { get; set; }
    public string KieuDieuChinh { get; set; } = null!;
    public decimal GiaTri { get; set; }
    public ChinhSachGia ChinhSachGia { get; set; } = null!;
    public LoaiPhong LoaiPhong { get; set; } = null!;
}

public sealed class TaiKhoan : EntityBase
{
    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }
    public string MatKhauHash { get; set; } = null!;
    public string LoaiTaiKhoan { get; set; } = null!;
    public string TrangThai { get; set; } = "HOAT_DONG";
    public bool EmailDaXacThuc { get; set; }
    public DateTime? LanDangNhapCuoi { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public KhachHang? KhachHang { get; set; }
    public NhanVien? NhanVien { get; set; }
    public ICollection<TaiKhoanVaiTro> TaiKhoanVaiTros { get; set; } = [];
}

public sealed class PhuongThucThanhToan : EntityBase
{
    public string MaPhuongThuc { get; set; } = null!;
    public string TenPhuongThuc { get; set; } = null!;
    public string TrangThai { get; set; } = "HOAT_DONG";
}

public sealed class PhieuCoc : EntityBase
{
    public string MaPhieuCoc { get; set; } = null!;
    public long DatPhongId { get; set; }
    public long? TaiKhoanThanhToanId { get; set; }
    public long? NhanVienXacNhanId { get; set; }
    public long? CaLamViecId { get; set; }
    public long PhuongThucId { get; set; }
    public decimal SoTien { get; set; }
    public string? CongThanhToan { get; set; }
    public string? MaGiaoDich { get; set; }
    public DateTime ThoiGianCoc { get; set; }
    public string TrangThai { get; set; } = "CHO_XU_LY";
    public string? GhiChu { get; set; }
}

public sealed class CaLamViec : EntityBase
{
    public long NhanVienId { get; set; }
    public DateTime BatDauLuc { get; set; }
    public DateTime? KetThucLuc { get; set; }
    public decimal TienDauCa { get; set; }
    public string TrangThai { get; set; } = "DANG_MO";
}

public sealed class VaiTro : EntityBase
{
    public string MaVaiTro { get; set; } = null!;
    public string TenVaiTro { get; set; } = null!;
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public ICollection<TaiKhoanVaiTro> TaiKhoanVaiTros { get; set; } = [];
    public ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = [];
}

public sealed class Quyen : EntityBase
{
    public string MaQuyen { get; set; } = null!;
    public string TenQuyen { get; set; } = null!;
    public string? NhomQuyen { get; set; }
    public string? MoTa { get; set; }
    public ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = [];
}

public sealed class TaiKhoanVaiTro
{
    public long TaiKhoanId { get; set; }
    public long VaiTroId { get; set; }
    public TaiKhoan TaiKhoan { get; set; } = null!;
    public VaiTro VaiTro { get; set; } = null!;
}

public sealed class VaiTroQuyen
{
    public long VaiTroId { get; set; }
    public long QuyenId { get; set; }
    public VaiTro VaiTro { get; set; } = null!;
    public Quyen Quyen { get; set; } = null!;
}

public sealed class ChucVu : EntityBase
{
    public string MaChucVu { get; set; } = null!;
    public string TenChucVu { get; set; } = null!;
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public ICollection<NhanVien> NhanViens { get; set; } = [];
}

public sealed class NhanVien : EntityBase
{
    public string MaNhanVien { get; set; } = null!;
    public long TaiKhoanId { get; set; }
    public long ChucVuId { get; set; }
    public string HoTen { get; set; } = null!;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public string? CccdHoChieu { get; set; }
    public string? DiaChi { get; set; }
    public DateOnly? NgayVaoLam { get; set; }
    public string TrangThai { get; set; } = "DANG_LAM";
    public DateTime CreatedAt { get; set; }
    public TaiKhoan TaiKhoan { get; set; } = null!;
    public ChucVu ChucVu { get; set; } = null!;
}

public sealed class KhachHang : EntityBase
{
    public string MaKhachHang { get; set; } = null!;
    public long? TaiKhoanId { get; set; }
    public string HoTen { get; set; } = null!;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public string? CccdHoChieu { get; set; }
    public string? DiaChi { get; set; }
    public string? GhiChu { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public TaiKhoan? TaiKhoan { get; set; }
    public ICollection<DatPhong> DatPhongs { get; set; } = [];
}

public sealed class LoaiPhong : EntityBase
{
    public string MaLoaiPhong { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;
    public int SoNguoiLonToiDa { get; set; }
    public int SoTreEmToiDa { get; set; }
    public decimal? DienTichM2 { get; set; }
    public decimal GiaCoBanDem { get; set; }
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public ICollection<Phong> Phongs { get; set; } = [];
}

public sealed class Phong : EntityBase
{
    public string MaPhong { get; set; } = null!;
    public long LoaiPhongId { get; set; }
    public string SoPhong { get; set; } = null!;
    public string? Tang { get; set; }
    public string? HuongPhong { get; set; }
    public string? LoaiView { get; set; }
    public string? ViTri { get; set; }
    public decimal PhuThuDemMacDinh { get; set; }
    public string TrangThai { get; set; } = "SAN_SANG";
    public string? MoTa { get; set; }
    public string? GhiChu { get; set; }
    public LoaiPhong LoaiPhong { get; set; } = null!;
    public ICollection<ChiTietDatPhong> ChiTietDatPhongs { get; set; } = [];
}

public sealed class DatPhong : EntityBase
{
    [System.ComponentModel.DataAnnotations.Schema.Column("chinh_sach_coc_id")]
    public long? ChinhSachCocId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("chinh_sach_huy_id")]
    public long? ChinhSachHuyId { get; set; }
    public string MaDatPhong { get; set; } = null!;
    public long KhachHangId { get; set; }
    public long? TaiKhoanTaoId { get; set; }
    public DateTime NgayDat { get; set; }
    public DateTime DuKienNhan { get; set; }
    public DateTime DuKienTra { get; set; }
    public int SoNguoiLon { get; set; }
    public int SoTreEm { get; set; }
    public string KenhDat { get; set; } = "WEB";
    public string TrangThai { get; set; } = "CHO_COC";
    public string? YeuCauDacBiet { get; set; }
    public decimal TongTienPhongGoc { get; set; }
    public decimal TongTienDuKien { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public KhachHang KhachHang { get; set; } = null!;
    public ICollection<ChiTietDatPhong> ChiTietDatPhongs { get; set; } = [];
}

public sealed class ChiTietDatPhong : EntityBase
{
    public long DatPhongId { get; set; }
    public long PhongId { get; set; }
    public int SoNguoiLon { get; set; }
    public int SoTreEm { get; set; }
    public decimal TienPhongDuKien { get; set; }
    public DateTime? NhanPhongLuc { get; set; }
    public DateTime? TraPhongLuc { get; set; }
    public string TrangThai { get; set; } = "CHO_COC";
    public string? GhiChu { get; set; }
    public DatPhong DatPhong { get; set; } = null!;
    public Phong Phong { get; set; } = null!;
}

public sealed class KhoaLichPhong : EntityBase
{
    public long ChiTietDatPhongId { get; set; }
    public long PhongId { get; set; }
    public DateOnly NgayLuuTru { get; set; }
    public string TrangThai { get; set; } = "HOLD";
    public DateTime? HetHanGiuCho { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GiaDemDatPhong : EntityBase
{
    public long ChiTietDatPhongId { get; set; }
    public DateOnly NgayLuuTru { get; set; }
    public decimal GiaCoBan { get; set; }
    public decimal PhuThuPhong { get; set; }
    public long? ChinhSachGiaId { get; set; }
    public decimal TienDieuChinh { get; set; }
    public decimal GiaSauDieuChinh { get; set; }
}

public sealed class Kho : EntityBase
{
    public string MaKho { get; set; } = null!;
    public string TenKho { get; set; } = null!;
    public string LoaiKho { get; set; } = "KHO_CHINH";
    public string? ViTri { get; set; }
    public decimal? NhietDoTu { get; set; }
    public decimal? NhietDoDen { get; set; }
    public string? GhiChu { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class NhatKyHeThong : EntityBase
{
    public long? TaiKhoanId { get; set; }
    public string HanhDong { get; set; } = null!;
    public string? DoiTuong { get; set; }
    public string? DoiTuongId { get; set; }
    public string? DuLieuCu { get; set; }
    public string? DuLieuMoi { get; set; }
    public string? DiaChiIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DanhMucHangHoa : EntityBase
{
    public string MaHang { get; set; } = null!;
    public string TenHang { get; set; } = null!;
    public string LoaiHang { get; set; } = null!;
    public long? NhomHangId { get; set; }
    public long DonViTinhId { get; set; }
    public decimal GiaVonThamChieu { get; set; }
    public decimal MucTonToiThieu { get; set; }
    public bool TheoDoiLo { get; set; }
    public bool TheoDoiHanSuDung { get; set; }
    public bool TheoDoiCaThe { get; set; }
    public string? DieuKienBaoQuan { get; set; }
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class NhaCungCap : EntityBase
{
    public string MaNhaCungCap { get; set; } = null!;
    public string TenNhaCungCap { get; set; } = null!;
    public string? NguoiLienHe { get; set; }
    public string? SoDienThoai { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public string? MaSoThue { get; set; }
    public string? GhiChu { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class NhomHang : EntityBase
{
    public string MaNhom { get; set; } = null!;
    public string TenNhom { get; set; } = null!;
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
}

public sealed class DonViTinh : EntityBase
{
    public string MaDonVi { get; set; } = null!;
    public string TenDonVi { get; set; } = null!;
    public string? MoTa { get; set; }
    public string TrangThai { get; set; } = "HOAT_DONG";
}

public sealed class PhieuNhap : EntityBase
{
    public string MaPhieuNhap { get; set; } = null!;
    public long NhaCungCapId { get; set; }
    public long KhoId { get; set; }
    public long NhanVienTaoId { get; set; }
    public long? NhanVienXacNhanId { get; set; }
    public DateTime NgayNhap { get; set; }
    public DateTime? XacNhanLuc { get; set; }
    public string? SoHoaDonNcc { get; set; }
    public DateOnly? NgayHoaDonNcc { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal ChietKhau { get; set; }
    public decimal TienThue { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = "NHAP";
    public string? GhiChu { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChiTietPhieuNhap> ChiTiet { get; set; } = [];
}

public sealed class ChiTietPhieuNhap : EntityBase
{
    public long PhieuNhapId { get; set; }
    public long DanhMucHangHoaId { get; set; }
    public decimal SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal ThanhTien { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class LoHang : EntityBase
{
    public string MaLo { get; set; } = null!;
    public long DanhMucHangHoaId { get; set; }
    public long ChiTietPhieuNhapId { get; set; }
    public long KhoId { get; set; }
    public DateOnly? NgaySanXuat { get; set; }
    public DateOnly? HanSuDung { get; set; }
    public decimal SoLuongBanDau { get; set; }
    public decimal DonGiaNhap { get; set; }
    public string TrangThai { get; set; } = "CON_HAN";
    public string? GhiChu { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PhieuXuat : EntityBase
{
    public string MaPhieuXuat { get; set; } = null!;
    public long KhoId { get; set; }
    public long NhanVienTaoId { get; set; }
    public long? NhanVienXacNhanId { get; set; }
    public string? NguoiNhan { get; set; }
    public string MucDich { get; set; } = null!;
    public string? LyDo { get; set; }
    public DateTime NgayXuat { get; set; }
    public DateTime? XacNhanLuc { get; set; }
    public string TrangThai { get; set; } = "NHAP";
    public string? GhiChu { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChiTietPhieuXuat> ChiTiet { get; set; } = [];
}

public sealed class ChiTietPhieuXuat : EntityBase
{
    public long PhieuXuatId { get; set; }
    public long DanhMucHangHoaId { get; set; }
    public decimal SoLuong { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class GiaoDichKho : EntityBase
{
    public string MaGiaoDich { get; set; } = null!;
    public long KhoId { get; set; }
    public long DanhMucHangHoaId { get; set; }
    public long? LoHangId { get; set; }
    public long? PhieuNhapId { get; set; }
    public long? ChiTietPhieuNhapId { get; set; }
    public long? PhieuXuatId { get; set; }
    public long? ChiTietPhieuXuatId { get; set; }
    public long? NhanVienId { get; set; }
    public string LoaiGiaoDich { get; set; } = null!;
    public decimal SoLuongThayDoi { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? ThamChieu { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class TaiSanCaThe : EntityBase
{
    public string MaTaiSan { get; set; } = null!;
    public long DanhMucHangHoaId { get; set; }
    public long? ChiTietPhieuNhapId { get; set; }
    public long? PhongId { get; set; }
    public long? KhoId { get; set; }
    public string? SoSerial { get; set; }
    public DateOnly? NgayMua { get; set; }
    public decimal GiaMua { get; set; }
    public DateOnly? HanBaoHanh { get; set; }
    public string TinhTrang { get; set; } = "TOT";
    public string TrangThaiSuDung { get; set; } = "TRONG_KHO";
    public string? ViTriKhac { get; set; }
    public string? GhiChu { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TinhTrangPhong : EntityBase
{
    public long PhongId { get; set; }
    public long? DatPhongId { get; set; }
    public long? NhanVienGhiNhanId { get; set; }
    public long? TaiSanCaTheId { get; set; }
    public string LoaiTinhTrang { get; set; } = null!;
    public string MucDo { get; set; } = "NHE";
    public string MoTa { get; set; } = null!;
    public string? AnhMinhChung { get; set; }
    public decimal ChiPhiDuKien { get; set; }
    public decimal ChiPhiThucTe { get; set; }
    public bool DoKhachGayRa { get; set; }
    public string TrangThaiXuLy { get; set; } = "MOI_GHI_NHAN";
    public DateTime GhiNhanLuc { get; set; }
    public DateTime? XuLyXongLuc { get; set; }
}

public sealed class CongViecBuongPhong : EntityBase
{
    public string MaCongViec { get; set; } = null!;
    public long PhongId { get; set; }
    public long? DatPhongId { get; set; }
    public string LoaiCongViec { get; set; } = null!;
    public long? NhanVienDuocGiaoId { get; set; }
    public long? NhanVienPhanCongId { get; set; }
    public string MucDoUuTien { get; set; } = "BINH_THUONG";
    public DateTime? BatDauDuKien { get; set; }
    public DateTime? BatDauThucTe { get; set; }
    public DateTime? HoanThanhLuc { get; set; }
    public string TrangThai { get; set; } = "CHO_XU_LY";
    public string? GhiChu { get; set; }
}
