using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

[ApiController, Authorize, RequirePermission("REPORT_READ"), Route("v1/reports")]
public sealed class OperationalReportsController(HotelDbContext db) : ControllerBase
{
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(DateOnly from, DateOnly to, CancellationToken ct)
    {
        Input.Range(from, to); var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        var bookings = await db.DatPhongs.Where(x => x.NgayDat >= start && x.NgayDat < end).GroupBy(x => x.TrangThai).Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(x => x.TongTienDuKien) }).ToListAsync(ct);
        return Ok(new { From = from, To = to, Bookings = bookings,
            Cancellations = await db.YeuCauHuyDatPhongs.Where(x => x.ThoiGianYeuCau >= start && x.ThoiGianYeuCau < end).GroupBy(x => x.TrangThai).Select(g => new { Status = g.Key, Count = g.Count(), ApprovedRefund = g.Sum(x => x.SoTienHoanDuyet) }).ToListAsync(ct),
            Deposits = await db.PhieuCocs.Where(x => x.ThoiGianCoc >= start && x.ThoiGianCoc < end && x.TrangThai == "DA_THANH_TOAN").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0,
            Refunds = await db.Set<PhieuHoanTien>().Where(x => x.ThoiGianHoan >= start && x.ThoiGianHoan < end && x.TrangThai == "THANH_CONG").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0 });
    }
    [HttpGet("services")]
    public async Task<IActionResult> Services(DateOnly from, DateOnly to, CancellationToken ct)
    {
        Input.Range(from, to); var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        return Ok(await (from usage in db.Set<SuDungDichVu>() join service in db.Set<DichVu>() on usage.DichVuId equals service.Id
            where usage.TrangThai == "HOAN_THANH" && usage.ThoiGianPhucVu >= start && usage.ThoiGianPhucVu < end
                // Bỏ lượt dùng có hóa đơn dịch vụ đã hủy – khớp với báo cáo doanh thu (cũng loại hóa đơn DA_HUY).
                && !db.Set<ChiTietHoaDonDichVu>().Any(l => l.SuDungDichVuId == usage.Id
                    && db.Set<HoaDonDichVu>().Any(i => i.Id == l.HoaDonDichVuId && i.TrangThai == "DA_HUY"))
            group usage by new { service.Id, service.TenDichVu } into g orderby g.Sum(x => x.ThanhTien) descending
            select new { g.Key.Id, Name = g.Key.TenDichVu, Requests = g.Count(), Quantity = g.Sum(x => x.SoLuong), Amount = g.Sum(x => x.ThanhTien) }).ToListAsync(ct));
    }
    [HttpGet("customers")]
    public async Task<IActionResult> Customers(DateOnly from, DateOnly to, CancellationToken ct)
    {
        Input.Range(from, to); var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        var customerIds = await db.DatPhongs.Where(x => x.DuKienNhan >= start && x.DuKienNhan < end && (x.TrangThai == "DANG_O" || x.TrangThai == "DA_TRA")).Select(x => x.KhachHangId).Distinct().ToListAsync(ct);
        var returning = await db.DatPhongs.Where(x => customerIds.Contains(x.KhachHangId) && x.DuKienNhan < start && x.TrangThai == "DA_TRA").Select(x => x.KhachHangId).Distinct().CountAsync(ct);
        var ratings = await db.Set<DanhGia>().Where(x => x.NgayDanhGia >= start && x.NgayDanhGia < end).Select(x => (double)x.DiemTong).ToListAsync(ct);
        return Ok(new { Guests = customerIds.Count, Returning = returning, FirstTime = customerIds.Count - returning, NewProfiles = await db.KhachHangs.CountAsync(x => x.CreatedAt >= start && x.CreatedAt < end, ct), Reviews = ratings.Count, AverageRating = ratings.Count == 0 ? 0 : ratings.Average() });
    }
    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory(DateOnly from, DateOnly to, long warehouseId, CancellationToken ct)
    {
        Input.Range(from, to); if (!await db.Khos.AnyAsync(x => x.Id == warehouseId, ct)) throw new ResourceNotFoundException("Warehouse was not found.");
        var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        var totals = await db.GiaoDichKhos.Where(x => x.KhoId == warehouseId && x.ThoiGian < end).GroupBy(x => x.DanhMucHangHoaId)
            .Select(g => new { ItemId = g.Key, Opening = g.Sum(x => x.ThoiGian < start ? x.SoLuongThayDoi : 0),
                Received = g.Sum(x => x.ThoiGian >= start && x.SoLuongThayDoi > 0 ? x.SoLuongThayDoi : 0),
                Issued = g.Sum(x => x.ThoiGian >= start && x.SoLuongThayDoi < 0 ? -x.SoLuongThayDoi : 0), Closing = g.Sum(x => x.SoLuongThayDoi) }).ToListAsync(ct);
        // Giá trị nhập = Σ thành tiền (SL × đơn giá) của phiếu nhập ĐÃ XÁC NHẬN trong kỳ, theo ngày xác nhận (= ngày hàng vào kho).
        // Chưa trừ chiết khấu / cộng thuế vì hai khoản này ghi chung cho cả phiếu (xem /reports/purchases).
        var purchaseValues = await (from line in db.Set<ChiTietPhieuNhap>()
                                    join order in db.PhieuNhaps on line.PhieuNhapId equals order.Id
                                    where order.KhoId == warehouseId && order.TrangThai == "DA_XAC_NHAN" && order.XacNhanLuc >= start && order.XacNhanLuc < end
                                    group line by line.DanhMucHangHoaId into g
                                    select new { ItemId = g.Key, Value = g.Sum(x => x.ThanhTien) }).ToListAsync(ct);
        var items = await db.DanhMucHangHoas.AsNoTracking().ToListAsync(ct);
        return Ok(items.Select(x => { var t = totals.SingleOrDefault(t => t.ItemId == x.Id); return new { ItemId = x.Id, x.MaHang, x.TenHang, Opening = t?.Opening ?? 0, Received = t?.Received ?? 0, Issued = t?.Issued ?? 0, Closing = t?.Closing ?? 0, Minimum = x.MucTonToiThieu, BelowMinimum = (t?.Closing ?? 0) < x.MucTonToiThieu,
            PurchaseValue = purchaseValues.Where(v => v.ItemId == x.Id).Sum(v => v.Value) }; }));
    }

    /// <summary>
    /// Chi phí nhập hàng trong kỳ theo nhà cung cấp: chỉ phiếu nhập đã xác nhận (theo ngày xác nhận); bỏ phiếu nháp / đã hủy.
    /// Đây là tiền khách sạn CHI RA – tách riêng, không gộp vào doanh thu và không liên quan ca thu ngân.
    /// </summary>
    [HttpGet("purchases")]
    public async Task<IActionResult> Purchases(DateOnly from, DateOnly to, long? warehouseId, CancellationToken ct)
    {
        Input.Range(from, to); var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        var rows = await db.PhieuNhaps.AsNoTracking()
            .Where(x => x.TrangThai == "DA_XAC_NHAN" && x.XacNhanLuc >= start && x.XacNhanLuc < end && (!warehouseId.HasValue || x.KhoId == warehouseId))
            .GroupBy(x => x.NhaCungCapId)
            .Select(g => new { SupplierId = g.Key, Orders = g.Count(), Goods = g.Sum(x => x.TongTienHang), Discount = g.Sum(x => x.ChietKhau), Tax = g.Sum(x => x.TienThue), Total = g.Sum(x => x.TongThanhToan) })
            .ToListAsync(ct);
        var ids = rows.Select(x => x.SupplierId).ToList();
        var names = await db.NhaCungCaps.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.TenNhaCungCap, ct);
        return Ok(new
        {
            From = from, To = to, WarehouseId = warehouseId,
            Orders = rows.Sum(x => x.Orders), Goods = rows.Sum(x => x.Goods), Discount = rows.Sum(x => x.Discount), Tax = rows.Sum(x => x.Tax), Total = rows.Sum(x => x.Total),
            Suppliers = rows.OrderByDescending(x => x.Total).Select(x => new { x.SupplierId, SupplierName = names.GetValueOrDefault(x.SupplierId, $"NCC #{x.SupplierId}"), x.Orders, x.Goods, x.Discount, x.Tax, x.Total })
        });
    }
    [HttpGet("revenue-breakdown")]
    public async Task<IActionResult> Revenue(DateOnly from, DateOnly to, CancellationToken ct)
    {
        Input.Range(from, to); var start = HotelTime.StartOfDayUtc(from); var end = HotelTime.StartOfDayUtc(to.AddDays(1));
        var room = await db.HoaDonPhongs.Where(x => x.NgayLap >= start && x.NgayLap < end && x.TrangThai != "DA_HUY" && x.TrangThai != "NHAP").Select(x => new { x.NgayLap, x.TongTien }).ToListAsync(ct);
        var services = await db.Set<HoaDonDichVu>().Where(x => x.NgayLap >= start && x.NgayLap < end && x.TrangThai != "DA_HUY" && x.TrangThai != "NHAP").Select(x => new { x.NgayLap, x.TongTien }).ToListAsync(ct);
        var payments = await db.ThanhToanHoaDonPhongs.Where(x => x.ThoiGianThanhToan >= start && x.ThoiGianThanhToan < end && x.TrangThai == "THANH_CONG").Select(x => new { x.PhuongThucId, x.SoTien }).ToListAsync(ct);
        var servicePayments = await db.Set<ThanhToanDichVu>().Where(x => x.ThoiGianThanhToan >= start && x.ThoiGianThanhToan < end && x.TrangThai == "THANH_CONG").Select(x => new { x.PhuongThucId, x.SoTien }).ToListAsync(ct);
        var byType = await (from line in db.ChiTietHoaDonPhongs join invoice in db.HoaDonPhongs on line.HoaDonPhongId equals invoice.Id join detail in db.ChiTietDatPhongs on line.ChiTietDatPhongId equals (long?)detail.Id join physical in db.Phongs on detail.PhongId equals physical.Id
            where line.LoaiDong == "TIEN_PHONG" && invoice.TrangThai != "DA_HUY" && invoice.TrangThai != "NHAP" && invoice.NgayLap >= start && invoice.NgayLap < end
            group line by physical.LoaiPhongId into g select new { RoomTypeId = g.Key, Revenue = g.Sum(x => x.ThanhTien) }).ToListAsync(ct);
        return Ok(new { Daily = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => { var d = from.AddDays(i); return new { Date = d, Rooms = room.Where(x => HotelTime.StayDate(x.NgayLap) == d).Sum(x => x.TongTien), Services = services.Where(x => HotelTime.StayDate(x.NgayLap) == d).Sum(x => x.TongTien) }; }),
            PaymentMethods = payments.Concat(servicePayments).GroupBy(x => x.PhuongThucId).Select(g => new { PaymentMethodId = g.Key, Collected = g.Sum(x => x.SoTien) }), RoomTypes = byType });
    }
    [HttpGet("operations")]
    public async Task<IActionResult> Operations(DateOnly date, CancellationToken ct)
    {
        var from = HotelTime.StartOfDayUtc(date); var to = from.AddDays(1);
        return Ok(new { Rooms = await db.Phongs.GroupBy(x => x.TrangThai).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct),
            Arrivals = await db.DatPhongs.CountAsync(x => x.DuKienNhan >= from && x.DuKienNhan < to && x.TrangThai == "BOOKED", ct),
            Departures = await db.DatPhongs.CountAsync(x => x.DuKienTra >= from && x.DuKienTra < to && x.TrangThai == "DANG_O", ct),
            PendingCancellations = await db.YeuCauHuyDatPhongs.CountAsync(x => x.TrangThai == "CHO_DUYET", ct) });
    }
}
