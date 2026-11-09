using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quanlychuoikhachsan.Domain.Entities;

/// <summary>
/// Nội dung hiển thị trên trang web "Thông tin &amp; chính sách" (bảng noi_dung_web, script add_web_content.sql).
/// Loai: CHINH_SACH (chính sách bổ sung), UU_DAI (ưu đãi), SU_KIEN (sự kiện đặc biệt).
/// Chính sách cọc / hủy / phụ thu / giá lấy trực tiếp từ các bảng chính sách, không nhập lại ở đây.
/// </summary>
[Table("noi_dung_web")]
public sealed class NoiDungWeb : EntityBase
{
    public static readonly string[] Types = ["CHINH_SACH", "UU_DAI", "SU_KIEN"];

    [Column("loai"), MaxLength(20)]
    public string Loai { get; set; } = null!;
    [Column("tieu_de"), MaxLength(200)]
    public string TieuDe { get; set; } = null!;
    [Column("noi_dung"), MaxLength(2000)]
    public string NoiDung { get; set; } = null!;
    /// <summary>Nhãn nhỏ trên thẻ, ví dụ "ƯU ĐÃI", "THÀNH VIÊN".</summary>
    [Column("nhan"), MaxLength(50)]
    public string? Nhan { get; set; }
    /// <summary>Dòng thời gian hiển thị tự do, ví dụ "Áp dụng quanh năm".</summary>
    [Column("thoi_gian_hien_thi"), MaxLength(150)]
    public string? ThoiGianHienThi { get; set; }
    [Column("ngay_su_kien")]
    public DateOnly? NgaySuKien { get; set; }
    /// <summary>Chỉ hiện trên web trong khoảng ngày này (null = không giới hạn).</summary>
    [Column("hien_tu_ngay")]
    public DateOnly? HienTuNgay { get; set; }
    [Column("hien_den_ngay")]
    public DateOnly? HienDenNgay { get; set; }
    [Column("thu_tu")]
    public int ThuTu { get; set; }
    [Column("trang_thai"), MaxLength(20)]
    public string TrangThai { get; set; } = "HOAT_DONG";
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
