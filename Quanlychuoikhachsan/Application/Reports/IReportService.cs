using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Reports;

public interface IReportService
{
    Task<DashboardResponse> DashboardAsync(ReportRange range, CancellationToken ct);
    Task<RevenueReportResponse> RevenueAsync(ReportRange range, CancellationToken ct);
    Task<OccupancyReportResponse> OccupancyAsync(ReportRange range, CancellationToken ct);
}

/// <summary>Tổng hợp doanh thu, công suất phòng và các chỉ số dashboard theo ngày.</summary>
public sealed class ReportService(HotelDbContext db) : IReportService
{
    public async Task<DashboardResponse> DashboardAsync(ReportRange range, CancellationToken ct)
    {
        var revenue = await RevenueAsync(range, ct); var occupancy = await OccupancyAsync(range, ct); var bookings = await db.DatPhongs.CountAsync(x => x.NgayDat >= range.From.ToDateTime(TimeOnly.MinValue) && x.NgayDat < range.To.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);
        var dues = await db.HoaDonPhongs.Where(x => x.TrangThai != "DA_THANH_TOAN" && x.TrangThai != "DA_HUY" && x.TrangThai != "NHAP")
            .Select(x => x.TongTien - (db.ApDungCocHoaDons.Where(d => d.HoaDonPhongId == x.Id).Sum(d => (decimal?)d.SoTienApDung) ?? 0)
                - (db.ThanhToanHoaDonPhongs.Where(p => p.HoaDonPhongId == x.Id && p.TrangThai == "THANH_CONG").Sum(p => (decimal?)p.SoTien) ?? 0)).ToListAsync(ct);
        var outstanding = dues.Sum(x => Math.Max(0, x));
        return new(range.From, range.To, revenue.RoomRevenue, revenue.ServiceRevenue, revenue.Compensation, outstanding, bookings, occupancy.OccupiedRoomNights, occupancy.AvailableRoomNights, occupancy.Occupancy, occupancy.Adr, occupancy.RevPar);
    }

    public async Task<RevenueReportResponse> RevenueAsync(ReportRange range, CancellationToken ct)
    {
        var from = range.From.ToDateTime(TimeOnly.MinValue); var to = range.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var invoices = await db.HoaDonPhongs.Where(x => x.NgayLap >= from && x.NgayLap < to && x.TrangThai != "DA_HUY" && x.TrangThai != "NHAP").ToListAsync(ct);
        var service = await db.Set<HoaDonDichVu>().Where(x => x.NgayLap >= from && x.NgayLap < to && x.TrangThai != "DA_HUY" && x.TrangThai != "NHAP").SumAsync(x => (decimal?)x.TongTien, ct) ?? 0;
        var room = invoices.Sum(x => x.TienPhong + x.TienPhuThu); var compensation = invoices.Sum(x => x.TienBoiThuong); var vat = invoices.Sum(x => x.TienThue);
        return new(range.From, range.To, room, service, compensation, vat, room + service + compensation + vat);
    }

    public async Task<OccupancyReportResponse> OccupancyAsync(ReportRange range, CancellationToken ct)
    {
        var days = Math.Max(1, range.To.DayNumber - range.From.DayNumber + 1); var rooms = await db.Phongs.CountAsync(x => x.TrangThai != "NGUNG_SU_DUNG", ct); var available = rooms * days;
        var sold = await (from night in db.GiaDemDatPhongs join detail in db.ChiTietDatPhongs on night.ChiTietDatPhongId equals detail.Id
            where night.NgayLuuTru >= range.From && night.NgayLuuTru <= range.To && (detail.TrangThai == "DANG_O" || detail.TrangThai == "DA_TRA")
            select night.GiaSauDieuChinh).ToListAsync(ct);
        var occupied = sold.Count;
        var roomRevenue = sold.Sum();
        var occupancy = available == 0 ? 0 : decimal.Round(occupied * 100m / available, 2); var adr = occupied == 0 ? 0 : decimal.Round(roomRevenue / occupied, 2); var revPar = available == 0 ? 0 : decimal.Round(roomRevenue / available, 2);
        return new(range.From, range.To, available, occupied, occupancy, adr, revPar);
    }

}
