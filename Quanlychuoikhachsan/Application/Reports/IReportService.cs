using Microsoft.EntityFrameworkCore;
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
        var outstanding = await db.HoaDonPhongs.Where(x => x.TrangThai != "DA_THANH_TOAN" && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.TongTien, ct) ?? 0;
        return new(range.From, range.To, revenue.RoomRevenue, revenue.ServiceRevenue, revenue.Compensation, outstanding, bookings, occupancy.OccupiedRoomNights, occupancy.AvailableRoomNights, occupancy.Occupancy, occupancy.Adr, occupancy.RevPar);
    }

    public async Task<RevenueReportResponse> RevenueAsync(ReportRange range, CancellationToken ct)
    {
        var from = range.From.ToDateTime(TimeOnly.MinValue); var to = range.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var invoices = await db.HoaDonPhongs.Where(x => x.NgayLap >= from && x.NgayLap < to && x.TrangThai != "DA_HUY").ToListAsync(ct);
        var service = await db.Set<ServiceInvoice>().Where(x => x.Date >= from && x.Date < to && x.Status != "DA_HUY").SumAsync(x => (decimal?)x.Total, ct) ?? 0;
        var room = invoices.Sum(x => x.TienPhong); var compensation = invoices.Sum(x => x.TienBoiThuong); var vat = invoices.Sum(x => x.TienThue);
        return new(range.From, range.To, room, service, compensation, vat, room + service + compensation + vat);
    }

    public async Task<OccupancyReportResponse> OccupancyAsync(ReportRange range, CancellationToken ct)
    {
        var days = Math.Max(1, range.To.DayNumber - range.From.DayNumber + 1); var rooms = await db.Phongs.CountAsync(x => x.TrangThai != "NGUNG_SU_DUNG", ct); var available = rooms * days;
        var occupied = await db.KhoaLichPhongs.CountAsync(x => x.NgayLuuTru >= range.From && x.NgayLuuTru <= range.To && x.TrangThai == "BOOKED", ct);
        var roomRevenue = await db.HoaDonPhongs.Where(x => x.NgayLap >= range.From.ToDateTime(TimeOnly.MinValue) && x.NgayLap < range.To.AddDays(1).ToDateTime(TimeOnly.MinValue) && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.TienPhong, ct) ?? 0;
        var occupancy = available == 0 ? 0 : decimal.Round(occupied * 100m / available, 2); var adr = occupied == 0 ? 0 : decimal.Round(roomRevenue / occupied, 2); var revPar = available == 0 ? 0 : decimal.Round(roomRevenue / available, 2);
        return new(range.From, range.To, available, occupied, occupancy, adr, revPar);
    }

    // SQL schema names are intentionally isolated here; replace this keyless projection with vw_cong_no_hoa_don_dich_vu mapping when service entities are added.
    public sealed class ServiceInvoice { public long Id { get; set; } public DateTime Date { get; set; } public decimal Total { get; set; } public string Status { get; set; } = null!; }
}
