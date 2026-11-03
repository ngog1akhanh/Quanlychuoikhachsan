using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Inventory;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class TestStore : IAsyncDisposable
{
    public HotelDbContext Db { get; }
    // Unit tests do not have a request execution context. Keep the selected actor
    // across awaited fixture helpers without HttpContextAccessor's AsyncLocal scope.
    public sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
    public TestHttpContextAccessor Http { get; } = new();
    public CurrentActor Actor { get; }
    public InventoryService Inventory => new(Db, Actor);
    public BookingService Bookings => new(Db, Actor, new NotificationWriter(Db));
    public TestStore(HotelDbContext? existing = null)
    {
        Db = existing ?? new(new DbContextOptionsBuilder<HotelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        UseAccount(1); Actor = new(Db, Http);
    }
    public void UseAccount(long id) => Http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) };
    public async Task SeedAsync()
    {
        Db.Add(new ChucVu { Id = 1, MaChucVu = "TEST", TenChucVu = "Test" });
        Db.Add(new TaiKhoan { Id = 1, Email = "staff@example.test", MatKhauHash = "hash", LoaiTaiKhoan = "NHAN_VIEN", TrangThai = "HOAT_DONG" });
        Db.Add(new TaiKhoan { Id = 2, Email = "customer@example.test", MatKhauHash = "hash", LoaiTaiKhoan = "KHACH_HANG", TrangThai = "HOAT_DONG" });
        Db.Add(new TaiKhoan { Id = 3, Email = "other@example.test", MatKhauHash = "hash", LoaiTaiKhoan = "KHACH_HANG", TrangThai = "HOAT_DONG" });
        Db.Add(new NhanVien { Id = 1, MaNhanVien = "NV1", TaiKhoanId = 1, ChucVuId = 1, HoTen = "Staff", TrangThai = "DANG_LAM" });
        Db.Add(new KhachHang { Id = 1, MaKhachHang = "KH1", TaiKhoanId = 2, HoTen = "Guest" });
        Db.Add(new Kho { Id = 1, MaKho = "K1", TenKho = "Warehouse" });
        Db.Add(new Kho { Id = 2, MaKho = "K2", TenKho = "Other warehouse" });
        Db.Add(new NhaCungCap { Id = 1, MaNhaCungCap = "NCC1", TenNhaCungCap = "Supplier" });
        Db.Add(new DonViTinh { Id = 1, MaDonVi = "CAI", TenDonVi = "Unit" });
        Db.Add(new DanhMucHangHoa { Id = 1, MaHang = "MILK", TenHang = "Milk", LoaiHang = "DO_UONG", DonViTinhId = 1, TheoDoiLo = true, TheoDoiHanSuDung = true });
        Db.Add(new DanhMucHangHoa { Id = 2, MaHang = "CHAIR", TenHang = "Chair", LoaiHang = "TAI_SAN", DonViTinhId = 1, TheoDoiCaThe = true });
        Db.Add(new DanhMucHangHoa { Id = 3, MaHang = "SOAP", TenHang = "Soap", LoaiHang = "VAT_TU", DonViTinhId = 1 });
        Db.Add(new PhuongThucThanhToan { Id = 1, MaPhuongThuc = "TIEN_MAT", TenPhuongThuc = "Cash" });
        Db.Add(new LoaiPhong { Id = 1, MaLoaiPhong = "STD", TenLoaiPhong = "Standard", SoNguoiLonToiDa = 2, SoTreEmToiDa = 1, GiaCoBanDem = 100 });
        Db.Add(new Phong { Id = 1, MaPhong = "R1", SoPhong = "101", LoaiPhongId = 1 });
        Db.Add(new Phong { Id = 2, MaPhong = "R2", SoPhong = "102", LoaiPhongId = 1 });
        Db.Add(new DichVu { Id = 1, MaDichVu = "SPA", TenDichVu = "Spa", DonViTinh = "visit", DonGia = 125 });
        await Db.SaveChangesAsync();
    }
    public async Task<DatPhong> SeedStayAsync(string status = "DANG_O")
    {
        var b = new DatPhong { Id = 1, MaDatPhong = "B1", KhachHangId = 1, TaiKhoanTaoId = 2, DuKienNhan = DateTime.UtcNow.AddHours(-1), DuKienTra = DateTime.UtcNow.AddDays(1), SoNguoiLon = 1, TrangThai = status,
            ChiTietDatPhongs = [new ChiTietDatPhong { Id = 1, PhongId = 1, SoNguoiLon = 1, TrangThai = status, NhanPhongLuc = DateTime.UtcNow }] };
        Db.Add(b); Db.Add(new GiaDemDatPhong { ChiTietDatPhongId = 1, NgayLuuTru = DateOnly.FromDateTime(DateTime.UtcNow), GiaCoBan = 100, GiaSauDieuChinh = 100 });
        await Db.SaveChangesAsync(); return b;
    }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
