using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Infrastructure.Persistence;

public static class OperationalModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<DepositPaymentOrder>();
        b.Entity<MaXacThuc>();
        b.Entity<KhachSan>();
        b.Entity<TienNghi>();
        b.Entity<LoaiPhongTienNghi>();
        b.Entity<HinhAnhLoaiPhong>();
        b.Entity<HinhAnhPhong>();
        b.Entity<ChinhSachCoc>();
        b.Entity<ChinhSachHuy>();
        b.Entity<ChiTietChinhSachHuy>();
        b.Entity<ChinhSachPhuThu>();
        b.Entity<LichSuTrangThaiDatPhong>();
        b.Entity<YeuCauThayDoiDatPhong>();
        b.Entity<LichSuDoiPhong>();
        b.Entity<KhachLuuTru>();
        b.Entity<DichVu>();
        b.Entity<SuDungDichVu>();
        b.Entity<HoaDonDichVu>();
        b.Entity<ChiTietHoaDonDichVu>();
        b.Entity<ThanhToanDichVu>();
        b.Entity<PhieuHoanTien>();
        b.Entity<DoiSoatCa>();
        b.Entity<ThongBao>();
        b.Entity<ThongBaoTaiKhoan>();
        b.Entity<CaiDatThongBao>();
        b.Entity<DanhGia>();
    }
}
