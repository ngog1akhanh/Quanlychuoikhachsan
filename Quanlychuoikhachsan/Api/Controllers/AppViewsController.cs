using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>
/// Danh sách chỉ đọc đã ghép sẵn tên hiển thị (mã booking, tên khách, số phòng, tên nhân viên...) cho App quản lý.
/// Các API nghiệp vụ gốc chỉ trả ID; màn hình WinForms cần tên để hiển thị và lọc.
/// Không ghi dữ liệu; quyền giống API gốc tương ứng. Thời điểm trả về là UTC (có hậu tố Z).
/// </summary>
[ApiController, Authorize, Route("v1/app")]
public sealed class AppViewsController(HotelDbContext db, CurrentActor actor) : ControllerBase
{
    // ---------------- Hóa đơn phòng ----------------
    [HttpGet("invoices"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Invoices([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<HoaDonPhong>().AsNoTracking()
            .Where(x => q.Status == null || x.TrangThai == q.Status)
            .Join(db.Set<DatPhong>(), i => i.DatPhongId, b => b.Id, (i, b) => new { i, b })
            .Where(x => s == null || x.i.MaHoaDon.Contains(s) || x.b.MaDatPhong.Contains(s) || x.b.KhachHang.HoTen.Contains(s)
                || (x.b.KhachHang.SoDienThoai != null && x.b.KhachHang.SoDienThoai.Contains(s)))
            .OrderByDescending(x => x.i.Id)
            .Select(x => new
            {
                x.i.Id, x.i.MaHoaDon, x.i.DatPhongId, x.b.MaDatPhong, x.b.KhachHang.HoTen, x.b.KhachHang.TaiKhoanId, x.i.NgayLap,
                x.i.TienPhong, x.i.TienPhuThu, x.i.TienBoiThuong, x.i.TienThue, x.i.TongTien, x.i.TrangThai, x.i.LyDoHuy,
                Deposit = db.Set<ApDungCocHoaDon>().Where(a => a.HoaDonPhongId == x.i.Id).Sum(a => (decimal?)a.SoTienApDung) ?? 0,
                Paid = db.Set<ThanhToanHoaDonPhong>().Where(p => p.HoaDonPhongId == x.i.Id && p.TrangThai == "THANH_CONG").Sum(p => (decimal?)p.SoTien) ?? 0,
                Rooms = db.Set<ChiTietDatPhong>().Where(d => d.DatPhongId == x.b.Id).Select(d => d.Phong.SoPhong).ToList()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, InvoiceCode = x.MaHoaDon, BookingId = x.DatPhongId, BookingCode = x.MaDatPhong, CustomerName = x.HoTen,
            HasAccount = x.TaiKhoanId.HasValue, Rooms = string.Join(", ", x.Rooms), IssuedAt = U(x.NgayLap),
            RoomAmount = x.TienPhong, Surcharge = x.TienPhuThu, Compensation = x.TienBoiThuong, Vat = x.TienThue, Total = x.TongTien,
            DepositApplied = x.Deposit, Paid = x.Paid, Due = Math.Max(0, x.TongTien - x.Deposit - x.Paid), Status = x.TrangThai, VoidReason = x.LyDoHuy
        }, ct));
    }

    // ---------------- Hóa đơn dịch vụ ----------------
    [HttpGet("service-invoices"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> ServiceInvoices([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<HoaDonDichVu>().AsNoTracking()
            .Where(x => q.Status == null || x.TrangThai == q.Status)
            .Join(db.Set<DatPhong>(), i => i.DatPhongId, b => b.Id, (i, b) => new { i, b })
            .Where(x => s == null || x.i.MaHoaDon.Contains(s) || x.b.MaDatPhong.Contains(s) || x.b.KhachHang.HoTen.Contains(s))
            .OrderByDescending(x => x.i.Id)
            .Select(x => new
            {
                x.i.Id, x.i.MaHoaDon, x.i.DatPhongId, x.b.MaDatPhong, x.b.KhachHang.HoTen, x.i.NgayLap, x.i.TongTien, x.i.TrangThai,
                Paid = db.Set<ThanhToanDichVu>().Where(p => p.HoaDonDichVuId == x.i.Id && p.TrangThai == "THANH_CONG").Sum(p => (decimal?)p.SoTien) ?? 0,
                Services = db.Set<ChiTietHoaDonDichVu>().Where(l => l.HoaDonDichVuId == x.i.Id)
                    .Join(db.Set<SuDungDichVu>(), l => l.SuDungDichVuId, u => u.Id, (l, u) => u.DichVuId)
                    .Join(db.Set<DichVu>(), id => id, d => d.Id, (id, d) => d.TenDichVu).ToList()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, InvoiceCode = x.MaHoaDon, BookingId = x.DatPhongId, BookingCode = x.MaDatPhong, CustomerName = x.HoTen,
            Services = string.Join(", ", x.Services), IssuedAt = U(x.NgayLap), Total = x.TongTien, Paid = x.Paid,
            Due = Math.Max(0, x.TongTien - x.Paid), Status = x.TrangThai
        }, ct));
    }

    // ---------------- Phiếu hoàn tiền ----------------
    [HttpGet("refunds"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Refunds([FromQuery] ListQuery q, CancellationToken ct)
    {
        var query = db.Set<PhieuHoanTien>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.MaPhieuHoan, x.PhieuCocId, x.ThanhToanHdpId, x.ThanhToanDvId, x.SoTien, x.LyDo, x.ThoiGianHoan, x.MaGiaoDich, x.TrangThai,
                Method = db.Set<PhuongThucThanhToan>().Where(m => m.Id == x.PhuongThucId).Select(m => m.TenPhuongThuc).FirstOrDefault(),
                BookingCode = x.PhieuCocId != null
                    ? db.Set<PhieuCoc>().Where(c => c.Id == x.PhieuCocId).Join(db.Set<DatPhong>(), c => c.DatPhongId, b => b.Id, (c, b) => b.MaDatPhong).FirstOrDefault()
                    : x.ThanhToanHdpId != null
                        ? db.Set<ThanhToanHoaDonPhong>().Where(p => p.Id == x.ThanhToanHdpId).Join(db.Set<HoaDonPhong>(), p => p.HoaDonPhongId, i => i.Id, (p, i) => i.DatPhongId)
                            .Join(db.Set<DatPhong>(), id => id, b => b.Id, (id, b) => b.MaDatPhong).FirstOrDefault()
                        : db.Set<ThanhToanDichVu>().Where(p => p.Id == x.ThanhToanDvId).Join(db.Set<HoaDonDichVu>(), p => p.HoaDonDichVuId, i => i.Id, (p, i) => i.DatPhongId)
                            .Join(db.Set<DatPhong>(), id => id, b => b.Id, (id, b) => b.MaDatPhong).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, RefundCode = x.MaPhieuHoan, BookingCode = x.BookingCode,
            Source = x.PhieuCocId != null ? "Tiền cọc" : x.ThanhToanHdpId != null ? "Thanh toán hóa đơn phòng" : "Thanh toán dịch vụ",
            Amount = x.SoTien, Method = x.Method, Reason = x.LyDo, Time = U(x.ThoiGianHoan), Reference = x.MaGiaoDich, Status = x.TrangThai
        }, ct));
    }

    // ---------------- Phiếu cọc ----------------
    [HttpGet("deposits"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Deposits([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<PhieuCoc>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status)
            .Join(db.Set<DatPhong>(), c => c.DatPhongId, b => b.Id, (c, b) => new { c, b })
            .Where(x => s == null || x.b.MaDatPhong.Contains(s) || x.b.KhachHang.HoTen.Contains(s) || x.c.MaPhieuCoc.Contains(s))
            .OrderByDescending(x => x.c.Id)
            .Select(x => new
            {
                x.c.Id, x.c.MaPhieuCoc, x.c.DatPhongId, x.b.MaDatPhong, BookingStatus = x.b.TrangThai, x.b.KhachHang.HoTen, x.c.SoTien, x.c.CongThanhToan,
                x.c.MaGiaoDich, x.c.ThoiGianCoc, x.c.TrangThai, x.c.PhuongThucId,
                Method = db.Set<PhuongThucThanhToan>().Where(m => m.Id == x.c.PhuongThucId).Select(m => m.TenPhuongThuc).FirstOrDefault(),
                Applied = db.Set<ApDungCocHoaDon>().Where(a => a.PhieuCocId == x.c.Id && db.Set<HoaDonPhong>().Any(i => i.Id == a.HoaDonPhongId && i.TrangThai != "DA_HUY")).Sum(a => (decimal?)a.SoTienApDung) ?? 0,
                Refunded = db.Set<PhieuHoanTien>().Where(r => r.PhieuCocId == x.c.Id && r.TrangThai != "DA_HUY").Sum(r => (decimal?)r.SoTien) ?? 0
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, DepositCode = x.MaPhieuCoc, BookingId = x.DatPhongId, BookingCode = x.MaDatPhong, x.BookingStatus, CustomerName = x.HoTen,
            Amount = x.SoTien, PaymentMethodId = x.PhuongThucId, Method = x.Method, Gateway = x.CongThanhToan, Reference = x.MaGiaoDich,
            PaidAt = U(x.ThoiGianCoc), Status = x.TrangThai, Applied = x.Applied, Refunded = x.Refunded,
            Refundable = Math.Max(0, x.SoTien - x.Applied - x.Refunded)
        }, ct));
    }

    // ---------------- Ca thu ngân ----------------
    [HttpGet("shifts"), RequirePermission("BILLING_READ")]
    public async Task<IActionResult> Shifts([FromQuery] ListQuery q, CancellationToken ct)
    {
        long? me = null;
        try { me = await actor.EmployeeIdAsync(ct); } catch (PermissionDeniedException) { }
        var query = db.Set<CaLamViec>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status).OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.NhanVienId, x.BatDauLuc, x.KetThucLuc, x.TienDauCa, x.TrangThai,
                Employee = db.Set<NhanVien>().Where(n => n.Id == x.NhanVienId).Select(n => n.HoTen).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, EmployeeId = x.NhanVienId, EmployeeName = x.Employee, Mine = x.NhanVienId == me, StartedAt = U(x.BatDauLuc), EndedAt = U(x.KetThucLuc),
            OpeningCash = x.TienDauCa, Status = x.TrangThai
        }, ct));
    }

    // ---------------- Buồng phòng ----------------
    [HttpGet("housekeeping-tasks"), RequirePermission("HOUSEKEEPING_READ")]
    public async Task<IActionResult> HousekeepingTasks([FromQuery] ListQuery q, bool mine = false, CancellationToken ct = default)
    {
        long? me = null;
        try { me = await actor.EmployeeIdAsync(ct); } catch (PermissionDeniedException) { }
        var s = q.Search?.Trim();
        var query = db.Set<CongViecBuongPhong>().AsNoTracking()
            .Where(x => (q.Status == null || x.TrangThai == q.Status) && (!mine || x.NhanVienDuocGiaoId == me))
            .Join(db.Set<Phong>(), t => t.PhongId, p => p.Id, (t, p) => new { t, p })
            .Where(x => s == null || x.p.SoPhong.Contains(s))
            .OrderBy(x => x.t.TrangThai == "HOAN_THANH").ThenByDescending(x => x.t.Id)
            .Select(x => new
            {
                x.t.Id, x.t.MaCongViec, x.t.PhongId, x.p.SoPhong, RoomStatus = x.p.TrangThai, x.t.DatPhongId, x.t.LoaiCongViec, x.t.MucDoUuTien, x.t.TrangThai, x.t.GhiChu,
                x.t.NhanVienDuocGiaoId, x.t.BatDauDuKien, x.t.BatDauThucTe, x.t.HoanThanhLuc,
                Assignee = db.Set<NhanVien>().Where(n => n.Id == x.t.NhanVienDuocGiaoId).Select(n => n.HoTen).FirstOrDefault(),
                BookingCode = db.Set<DatPhong>().Where(b => b.Id == x.t.DatPhongId).Select(b => b.MaDatPhong).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, TaskCode = x.MaCongViec, RoomId = x.PhongId, RoomNumber = x.SoPhong, x.RoomStatus, BookingId = x.DatPhongId, x.BookingCode,
            Type = x.LoaiCongViec, Priority = x.MucDoUuTien, Status = x.TrangThai, Note = x.GhiChu, AssigneeId = x.NhanVienDuocGiaoId, AssigneeName = x.Assignee,
            Mine = x.NhanVienDuocGiaoId == me, PlannedStart = U(x.BatDauDuKien), StartedAt = U(x.BatDauThucTe), CompletedAt = U(x.HoanThanhLuc)
        }, ct));
    }

    [HttpGet("room-conditions"), RequirePermission("HOUSEKEEPING_READ")]
    public async Task<IActionResult> RoomConditions([FromQuery] ListQuery q, CancellationToken ct)
    {
        var query = db.Set<TinhTrangPhong>().AsNoTracking().Where(x => q.Status == null || x.TrangThaiXuLy == q.Status)
            .Join(db.Set<Phong>(), c => c.PhongId, p => p.Id, (c, p) => new { c, p })
            .OrderBy(x => x.c.TrangThaiXuLy == "DA_XU_LY").ThenByDescending(x => x.c.Id)
            .Select(x => new
            {
                x.c.Id, x.c.PhongId, x.p.SoPhong, RoomStatus = x.p.TrangThai, x.c.DatPhongId, x.c.LoaiTinhTrang, x.c.MucDo, x.c.MoTa, x.c.ChiPhiDuKien, x.c.ChiPhiThucTe,
                x.c.DoKhachGayRa, x.c.TrangThaiXuLy, x.c.GhiNhanLuc, x.c.XuLyXongLuc,
                BookingCode = db.Set<DatPhong>().Where(b => b.Id == x.c.DatPhongId).Select(b => b.MaDatPhong).FirstOrDefault(),
                Reporter = db.Set<NhanVien>().Where(n => n.Id == x.c.NhanVienGhiNhanId).Select(n => n.HoTen).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, RoomId = x.PhongId, RoomNumber = x.SoPhong, x.RoomStatus, BookingId = x.DatPhongId, x.BookingCode, Type = x.LoaiTinhTrang, Severity = x.MucDo,
            Description = x.MoTa, EstimatedCost = x.ChiPhiDuKien, ActualCost = x.ChiPhiThucTe, CausedByGuest = x.DoKhachGayRa, Status = x.TrangThaiXuLy,
            ReportedBy = x.Reporter, ReportedAt = U(x.GhiNhanLuc), ResolvedAt = U(x.XuLyXongLuc)
        }, ct));
    }

    /// <summary>Nhân viên đang làm để phân công công việc buồng phòng.</summary>
    [HttpGet("employees"), RequirePermission("HOUSEKEEPING_READ")]
    public async Task<IActionResult> Employees(CancellationToken ct) => Ok(await db.Set<NhanVien>().AsNoTracking()
        .Where(x => x.TrangThai == "DANG_LAM" && x.TaiKhoan.TrangThai == "HOAT_DONG").OrderBy(x => x.HoTen)
        .Select(x => new { x.Id, Code = x.MaNhanVien, FullName = x.HoTen, Position = x.ChucVu.TenChucVu, PositionCode = x.ChucVu.MaChucVu }).ToListAsync(ct));

    // ---------------- Dịch vụ ----------------
    [HttpGet("service-requests"), RequirePermission("SERVICE_READ")]
    public async Task<IActionResult> ServiceRequests([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<SuDungDichVu>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status)
            .Join(db.Set<DatPhong>(), u => u.DatPhongId, b => b.Id, (u, b) => new { u, b })
            .Join(db.Set<DichVu>(), x => x.u.DichVuId, d => d.Id, (x, d) => new { x.u, x.b, d })
            .Where(x => s == null || x.b.MaDatPhong.Contains(s) || x.b.KhachHang.HoTen.Contains(s) || x.d.TenDichVu.Contains(s))
            .OrderBy(x => x.u.TrangThai == "HOAN_THANH" || x.u.TrangThai == "DA_HUY").ThenByDescending(x => x.u.Id)
            .Select(x => new
            {
                x.u.Id, x.u.MaSuDung, x.u.DatPhongId, x.b.MaDatPhong, x.b.KhachHang.HoTen, x.u.PhongId, x.d.TenDichVu, x.d.DonViTinh,
                x.u.SoLuong, x.u.DonGia, x.u.ThanhTien, x.u.TrangThai, x.u.GhiChu, x.u.ThoiGianYeuCau, x.u.ThoiGianPhucVu,
                Room = db.Set<Phong>().Where(p => p.Id == x.u.PhongId).Select(p => p.SoPhong).FirstOrDefault(),
                InvoiceId = db.Set<ChiTietHoaDonDichVu>().Where(l => l.SuDungDichVuId == x.u.Id).Select(l => (long?)l.HoaDonDichVuId).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, RequestCode = x.MaSuDung, BookingId = x.DatPhongId, BookingCode = x.MaDatPhong, CustomerName = x.HoTen, RoomId = x.PhongId, RoomNumber = x.Room,
            ServiceName = x.TenDichVu, Unit = x.DonViTinh, Quantity = x.SoLuong, UnitPrice = x.DonGia, Amount = x.ThanhTien, Status = x.TrangThai,
            Note = x.GhiChu, RequestedAt = U(x.ThoiGianYeuCau), ServedAt = U(x.ThoiGianPhucVu), x.InvoiceId
        }, ct));
    }

    /// <summary>Booking đang lưu trú (kèm phòng) để tạo yêu cầu dịch vụ.</summary>
    [HttpGet("active-stays"), RequirePermission("SERVICE_READ")]
    public async Task<IActionResult> ActiveStays(CancellationToken ct) => Ok(await db.Set<DatPhong>().AsNoTracking().Where(x => x.TrangThai == "DANG_O").OrderBy(x => x.MaDatPhong)
        .Select(x => new
        {
            x.Id, BookingCode = x.MaDatPhong, CustomerName = x.KhachHang.HoTen,
            Rooms = x.ChiTietDatPhongs.Where(d => d.TrangThai == "DANG_O").Select(d => new { RoomId = d.PhongId, RoomNumber = d.Phong.SoPhong }).ToList()
        }).ToListAsync(ct));

    // ---------------- Khách hàng & đánh giá ----------------
    [HttpGet("customers"), RequirePermission("CUSTOMER_READ")]
    public async Task<IActionResult> Customers([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<KhachHang>().AsNoTracking()
            .Where(x => s == null || x.HoTen.Contains(s) || x.MaKhachHang.Contains(s) || (x.SoDienThoai != null && x.SoDienThoai.Contains(s))
                || (x.Email != null && x.Email.Contains(s)) || (x.CccdHoChieu != null && x.CccdHoChieu.Contains(s)))
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.MaKhachHang, x.HoTen, x.SoDienThoai, x.Email, x.CccdHoChieu, x.NgaySinh, x.GioiTinh, x.DiaChi, x.GhiChu, x.TaiKhoanId, x.CreatedAt,
                Bookings = x.DatPhongs.Count(),
                Stays = x.DatPhongs.Count(b => b.TrangThai == "DA_TRA"),
                LastStay = x.DatPhongs.Where(b => b.TrangThai == "DA_TRA" || b.TrangThai == "DANG_O").Max(b => (DateTime?)b.DuKienNhan),
                Spent = x.DatPhongs.Where(b => b.TrangThai == "DA_TRA").Sum(b => (decimal?)b.TongTienDuKien) ?? 0
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, CustomerCode = x.MaKhachHang, FullName = x.HoTen, PhoneNumber = x.SoDienThoai, x.Email, IdentityNumber = x.CccdHoChieu,
            BirthDate = x.NgaySinh, Gender = x.GioiTinh, Address = x.DiaChi, Note = x.GhiChu, HasAccount = x.TaiKhoanId.HasValue, CreatedAt = U(x.CreatedAt),
            x.Bookings, x.Stays, LastStay = U(x.LastStay), TotalSpent = x.Spent
        }, ct));
    }

    [HttpGet("reviews"), RequirePermission("REVIEW_MANAGE")]
    public async Task<IActionResult> Reviews([FromQuery] ListQuery q, CancellationToken ct)
    {
        var query = db.Set<DanhGia>().AsNoTracking().Where(x => q.Status == null || x.TrangThai == q.Status)
            .Join(db.Set<DatPhong>(), r => r.DatPhongId, b => b.Id, (r, b) => new { r, b })
            .OrderByDescending(x => x.r.Id)
            .Select(x => new
            {
                x.r.Id, x.r.DatPhongId, x.b.MaDatPhong, x.b.KhachHang.HoTen, x.r.DiemTong, x.r.DiemPhong, x.r.DiemDichVu, x.r.NoiDung,
                x.r.NgayDanhGia, x.r.TrangThai, x.r.PhanHoiKhachSan, x.r.NgayPhanHoi
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, BookingId = x.DatPhongId, BookingCode = x.MaDatPhong, CustomerName = x.HoTen, Rating = (int)x.DiemTong, RoomRating = (int?)x.DiemPhong,
            ServiceRating = (int?)x.DiemDichVu, Content = x.NoiDung, CreatedAt = U(x.NgayDanhGia), Status = x.TrangThai, Reply = x.PhanHoiKhachSan, RepliedAt = U(x.NgayPhanHoi)
        }, ct));
    }

    // ---------------- Nhân viên ----------------
    [HttpGet("staff"), RequirePermission("STAFF_READ")]
    public async Task<IActionResult> Staff([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<NhanVien>().AsNoTracking()
            .Where(x => (q.Status == null || x.TrangThai == q.Status) && (s == null || x.HoTen.Contains(s) || x.MaNhanVien.Contains(s) || (x.Email != null && x.Email.Contains(s))))
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.MaNhanVien, x.HoTen, x.ChucVuId, Position = x.ChucVu.TenChucVu, x.Email, x.SoDienThoai, x.DiaChi, x.NgaySinh, x.NgayVaoLam, x.TrangThai,
                x.TaiKhoanId, AccountStatus = x.TaiKhoan.TrangThai, LastLogin = x.TaiKhoan.LanDangNhapCuoi,
                Roles = x.TaiKhoan.TaiKhoanVaiTros.Select(r => r.VaiTro.TenVaiTro).ToList(),
                RoleIds = x.TaiKhoan.TaiKhoanVaiTros.Select(r => r.VaiTroId).ToList()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, StaffCode = x.MaNhanVien, FullName = x.HoTen, PositionId = x.ChucVuId, x.Position, x.Email, PhoneNumber = x.SoDienThoai, Address = x.DiaChi,
            BirthDate = x.NgaySinh, StartDate = x.NgayVaoLam, Status = x.TrangThai, AccountId = x.TaiKhoanId, x.AccountStatus, LastLogin = U(x.LastLogin),
            Roles = string.Join(", ", x.Roles), x.RoleIds
        }, ct));
    }

    // ---------------- Chính sách ----------------
    /// <summary>Tất cả chính sách hủy (kể cả đã ngừng) để quản lý bật/tắt lại.</summary>
    [HttpGet("cancellation-policies"), RequirePermission("PRICING_WRITE")]
    public async Task<IActionResult> CancellationPolicies(CancellationToken ct) => Ok(await db.Set<ChinhSachHuy>().AsNoTracking().OrderByDescending(x => x.Id)
        .Select(x => new
        {
            x.Id, Name = x.TenChinhSach, Description = x.MoTa, Status = x.TrangThai,
            Rules = db.Set<ChiTietChinhSachHuy>().Where(d => d.ChinhSachHuyId == x.Id).OrderByDescending(d => d.SoGioTruocToiThieu)
                .Select(d => new { MinimumHours = d.SoGioTruocToiThieu, RefundPercent = d.PhanTramHoan, FixedFee = d.PhiHuyCoDinh }).ToList()
        }).ToListAsync(ct));

    // ---------------- Kho ----------------
    [HttpGet("assets"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Assets([FromQuery] ListQuery q, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var query = db.Set<TaiSanCaThe>().AsNoTracking()
            .Where(x => q.Status == null || x.TinhTrang == q.Status)
            .Join(db.Set<DanhMucHangHoa>(), a => a.DanhMucHangHoaId, i => i.Id, (a, i) => new { a, i })
            .Where(x => s == null || x.a.MaTaiSan.Contains(s) || x.i.TenHang.Contains(s) || (x.a.SoSerial != null && x.a.SoSerial.Contains(s)))
            .OrderByDescending(x => x.a.Id)
            .Select(x => new
            {
                x.a.Id, x.a.MaTaiSan, x.i.TenHang, x.a.SoSerial, x.a.PhongId, x.a.KhoId, x.a.ViTriKhac, x.a.TinhTrang, x.a.TrangThaiSuDung, x.a.GiaMua, x.a.NgayMua, x.a.GhiChu,
                Room = db.Set<Phong>().Where(p => p.Id == x.a.PhongId).Select(p => p.SoPhong).FirstOrDefault(),
                Warehouse = db.Set<Kho>().Where(k => k.Id == x.a.KhoId).Select(k => k.TenKho).FirstOrDefault()
            });
        return Ok(await PageAsync(query, q, x => new
        {
            x.Id, AssetCode = x.MaTaiSan, ItemName = x.TenHang, Serial = x.SoSerial, RoomId = x.PhongId, RoomNumber = x.Room, WarehouseId = x.KhoId,
            WarehouseName = x.Warehouse, OtherLocation = x.ViTriKhac,
            Location = x.Room != null ? "Phòng " + x.Room : x.Warehouse ?? x.ViTriKhac ?? "",
            Condition = x.TinhTrang, Usage = x.TrangThaiSuDung, PurchasePrice = x.GiaMua, PurchaseDate = x.NgayMua, Note = x.GhiChu
        }, ct));
    }

    [HttpGet("inventory-items"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> InventoryItems([FromQuery] ListQuery q, long? warehouseId, CancellationToken ct)
    {
        var s = q.Search?.Trim();
        var rows = await db.Set<DanhMucHangHoa>().AsNoTracking()
            .Where(x => (q.Status == null || x.TrangThai == q.Status) && (s == null || x.TenHang.Contains(s) || x.MaHang.Contains(s)))
            .OrderBy(x => x.MaHang)
            .Select(x => new
            {
                x.Id, x.MaHang, x.TenHang, x.LoaiHang, x.NhomHangId, x.DonViTinhId, x.GiaVonThamChieu, x.MucTonToiThieu, x.TheoDoiLo, x.TheoDoiHanSuDung,
                x.TheoDoiCaThe, x.DieuKienBaoQuan, x.MoTa, x.TrangThai,
                Unit = db.Set<DonViTinh>().Where(u => u.Id == x.DonViTinhId).Select(u => u.TenDonVi).FirstOrDefault(),
                Group = db.Set<NhomHang>().Where(g => g.Id == x.NhomHangId).Select(g => g.TenNhom).FirstOrDefault(),
                Balance = db.Set<GiaoDichKho>().Where(g => g.DanhMucHangHoaId == x.Id && (!warehouseId.HasValue || g.KhoId == warehouseId)).Sum(g => (decimal?)g.SoLuongThayDoi) ?? 0
            }).ToListAsync(ct);
        return Ok(rows.Select(x => new
        {
            x.Id, Code = x.MaHang, Name = x.TenHang, Type = x.LoaiHang, GroupId = x.NhomHangId, GroupName = x.Group, UnitId = x.DonViTinhId, UnitName = x.Unit,
            ReferenceCost = x.GiaVonThamChieu, MinimumStock = x.MucTonToiThieu, TrackLot = x.TheoDoiLo, TrackExpiry = x.TheoDoiHanSuDung,
            TrackIndividual = x.TheoDoiCaThe, StorageCondition = x.DieuKienBaoQuan, Description = x.MoTa, Status = x.TrangThai,
            x.Balance, BelowMinimum = x.Balance < x.MucTonToiThieu
        }));
    }

    /// <summary>Danh sách phòng tối giản cho nhân viên kho (chuyển tài sản vào phòng).</summary>
    [HttpGet("rooms"), RequirePermission("INVENTORY_READ")]
    public async Task<IActionResult> Rooms(CancellationToken ct) => Ok(await db.Set<Phong>().AsNoTracking().OrderBy(x => x.SoPhong)
        .Select(x => new { x.Id, RoomNumber = x.SoPhong, Floor = x.Tang, Status = x.TrangThai }).ToListAsync(ct));

    // ---------------- Hạ tầng ----------------
    private static DateTime U(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? U(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static async Task<object> PageAsync<T, TResult>(IQueryable<T> query, ListQuery q, Func<T, TResult> map, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(q.Offset).Take(q.Size).ToListAsync(ct);
        return new { Items = rows.Select(map).ToList(), Total = total, Page = Math.Clamp(q.Page, 1, 100000), PageSize = q.Size };
    }
}
