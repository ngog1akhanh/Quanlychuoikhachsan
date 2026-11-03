using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Reports;

// Thống kê hóa đơn (yêu cầu của giảng viên):
//  1. Tổng tiền hóa đơn theo tháng / quý / năm / các năm
//  2. Tổng tiền hóa đơn theo nhân viên lập, theo quý / năm
//  3. Doanh thu theo sản phẩm (dịch vụ + loại phòng) theo năm
// Quy ước:
//  - "Tổng tiền hóa đơn" = tong_tien của hóa đơn phòng + hóa đơn dịch vụ đã lập (bỏ NHAP và DA_HUY).
//  - Nhóm theo ngày lập tính bằng giờ Việt Nam (ngay_lap lưu UTC).

public sealed record InvoicePeriodRow(string Period, string Label, int Year, int InvoiceCount, decimal RoomTotal, decimal ServiceTotal, decimal Total);
public sealed record InvoicePeriodStats(string GroupBy, int FromYear, int ToYear, int InvoiceCount, decimal RoomTotal, decimal ServiceTotal, decimal Total, IReadOnlyList<InvoicePeriodRow> Periods);

public sealed record StaffPeriodRow(long? EmployeeId, string EmployeeCode, string EmployeeName, string Period, string Label, int InvoiceCount, decimal RoomTotal, decimal ServiceTotal, decimal Total);
public sealed record StaffTotalRow(long? EmployeeId, string EmployeeCode, string EmployeeName, int InvoiceCount, decimal Total);
public sealed record StaffStats(string GroupBy, int FromYear, int ToYear, IReadOnlyList<string> Periods, IReadOnlyList<StaffTotalRow> Staff, IReadOnlyList<StaffPeriodRow> Rows);

public sealed record ProductYearRow(int Year, string Kind, long ProductId, string Name, string Unit, decimal Quantity, decimal Amount);
public sealed record ProductStats(int FromYear, int ToYear, IReadOnlyList<int> Years, decimal ServiceTotal, decimal RoomTotal, IReadOnlyList<ProductYearRow> Rows);

public interface IInvoiceStatsService
{
    Task<InvoicePeriodStats> ByPeriodAsync(int fromYear, int toYear, string groupBy, CancellationToken ct);
    Task<StaffStats> ByStaffAsync(int fromYear, int toYear, string groupBy, CancellationToken ct);
    Task<ProductStats> ByProductAsync(int fromYear, int toYear, CancellationToken ct);
}

public sealed class InvoiceStatsService(HotelDbContext db) : IInvoiceStatsService
{
    private const int MaxYears = 10;

    // Một hóa đơn (phòng hoặc dịch vụ) đã quy về giờ VN
    private sealed record Inv(DateTime Local, decimal Total, long? EmployeeId, bool IsRoom);

    public async Task<InvoicePeriodStats> ByPeriodAsync(int fromYear, int toYear, string groupBy, CancellationToken ct)
    {
        groupBy = Normalize(groupBy, "month", "quarter", "year");
        var invoices = await LoadInvoicesAsync(fromYear, toYear, ct);
        var groups = invoices.ToLookup(x => Key(x.Local, groupBy));
        var periods = AllPeriods(fromYear, toYear, groupBy).Select(p =>
        {
            var g = groups[p.Key].ToList();
            var room = g.Where(x => x.IsRoom).Sum(x => x.Total);
            var service = g.Where(x => !x.IsRoom).Sum(x => x.Total);
            return new InvoicePeriodRow(p.Key, p.Label, p.Year, g.Count, room, service, room + service);
        }).ToList();
        return new InvoicePeriodStats(groupBy, fromYear, toYear, invoices.Count,
            periods.Sum(x => x.RoomTotal), periods.Sum(x => x.ServiceTotal), periods.Sum(x => x.Total), periods);
    }

    public async Task<StaffStats> ByStaffAsync(int fromYear, int toYear, string groupBy, CancellationToken ct)
    {
        groupBy = Normalize(groupBy, "quarter", "year");
        var invoices = await LoadInvoicesAsync(fromYear, toYear, ct);
        var staff = await db.NhanViens.AsNoTracking().Select(x => new { x.Id, x.MaNhanVien, x.HoTen }).ToDictionaryAsync(x => x.Id, ct);
        (string Code, string Name) Who(long? id) => id is { } v && staff.TryGetValue(v, out var s) ? (s.MaNhanVien, s.HoTen) : ("—", "Không xác định (hệ thống/website)");

        var periods = AllPeriods(fromYear, toYear, groupBy).ToList();
        var labels = periods.ToDictionary(x => x.Key, x => x.Label);
        var order = periods.Select((p, i) => (p.Key, i)).ToDictionary(x => x.Key, x => x.i);

        var rows = invoices.GroupBy(x => (x.EmployeeId, Period: Key(x.Local, groupBy)))
            .Select(g =>
            {
                var (code, name) = Who(g.Key.EmployeeId);
                var room = g.Where(x => x.IsRoom).Sum(x => x.Total);
                var service = g.Where(x => !x.IsRoom).Sum(x => x.Total);
                return new StaffPeriodRow(g.Key.EmployeeId, code, name, g.Key.Period, labels[g.Key.Period], g.Count(), room, service, room + service);
            })
            .OrderBy(x => order[x.Period]).ThenByDescending(x => x.Total)
            .ToList();
        var totals = rows.GroupBy(x => x.EmployeeId)
            .Select(g => new StaffTotalRow(g.Key, g.First().EmployeeCode, g.First().EmployeeName, g.Sum(x => x.InvoiceCount), g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Total).ToList();
        return new StaffStats(groupBy, fromYear, toYear, periods.Select(x => x.Label).ToList(), totals, rows);
    }

    public async Task<ProductStats> ByProductAsync(int fromYear, int toYear, CancellationToken ct)
    {
        var (start, end) = Range(fromYear, toYear);

        // Dịch vụ: dòng hóa đơn dịch vụ -> lần sử dụng -> danh mục dịch vụ
        var serviceLines = await (
            from line in db.Set<ChiTietHoaDonDichVu>()
            join inv in db.Set<HoaDonDichVu>() on line.HoaDonDichVuId equals inv.Id
            join use in db.Set<SuDungDichVu>() on line.SuDungDichVuId equals use.Id
            join svc in db.Set<DichVu>() on use.DichVuId equals svc.Id
            where inv.TrangThai != "NHAP" && inv.TrangThai != "DA_HUY" && inv.NgayLap >= start && inv.NgayLap < end
            select new { inv.NgayLap, svc.Id, svc.TenDichVu, svc.DonViTinh, use.SoLuong, line.ThanhTien }).AsNoTracking().ToListAsync(ct);

        // Loại phòng: dòng TIEN_PHONG của hóa đơn phòng -> chi tiết đặt phòng -> phòng -> loại phòng (1 dòng = 1 đêm)
        var roomLines = await (
            from line in db.ChiTietHoaDonPhongs
            join inv in db.HoaDonPhongs on line.HoaDonPhongId equals inv.Id
            join detail in db.ChiTietDatPhongs on line.ChiTietDatPhongId equals (long?)detail.Id
            join room in db.Phongs on detail.PhongId equals room.Id
            join type in db.LoaiPhongs on room.LoaiPhongId equals type.Id
            where line.LoaiDong == "TIEN_PHONG" && inv.TrangThai != "NHAP" && inv.TrangThai != "DA_HUY" && inv.NgayLap >= start && inv.NgayLap < end
            select new { inv.NgayLap, type.Id, type.TenLoaiPhong, line.SoLuong, line.ThanhTien }).AsNoTracking().ToListAsync(ct);

        var services = serviceLines
            .GroupBy(x => (Year: HotelTime.Local(x.NgayLap).Year, x.Id, x.TenDichVu, x.DonViTinh))
            .Select(g => new ProductYearRow(g.Key.Year, "DICH_VU", g.Key.Id, g.Key.TenDichVu, g.Key.DonViTinh, g.Sum(x => x.SoLuong), g.Sum(x => x.ThanhTien)));
        var rooms = roomLines
            .GroupBy(x => (Year: HotelTime.Local(x.NgayLap).Year, x.Id, x.TenLoaiPhong))
            .Select(g => new ProductYearRow(g.Key.Year, "LOAI_PHONG", g.Key.Id, g.Key.TenLoaiPhong, "đêm", g.Sum(x => x.SoLuong), g.Sum(x => x.ThanhTien)));
        var rows = services.Concat(rooms)
            .Where(x => x.Year >= fromYear && x.Year <= toYear)
            .OrderByDescending(x => x.Year).ThenBy(x => x.Kind).ThenByDescending(x => x.Amount).ToList();
        return new ProductStats(fromYear, toYear, Enumerable.Range(fromYear, toYear - fromYear + 1).ToList(),
            rows.Where(x => x.Kind == "DICH_VU").Sum(x => x.Amount), rows.Where(x => x.Kind == "LOAI_PHONG").Sum(x => x.Amount), rows);
    }

    // ------------------------------------------------------------------
    private async Task<List<Inv>> LoadInvoicesAsync(int fromYear, int toYear, CancellationToken ct)
    {
        var (start, end) = Range(fromYear, toYear);
        var rooms = await db.HoaDonPhongs.AsNoTracking()
            .Where(x => x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY" && x.NgayLap >= start && x.NgayLap < end)
            .Select(x => new { x.NgayLap, x.TongTien, x.NhanVienLapId }).ToListAsync(ct);
        var services = await db.Set<HoaDonDichVu>().AsNoTracking()
            .Where(x => x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY" && x.NgayLap >= start && x.NgayLap < end)
            .Select(x => new { x.NgayLap, x.TongTien, x.NhanVienLapId }).ToListAsync(ct);
        return rooms.Select(x => new Inv(HotelTime.Local(x.NgayLap), x.TongTien, x.NhanVienLapId, true))
            .Concat(services.Select(x => new Inv(HotelTime.Local(x.NgayLap), x.TongTien, x.NhanVienLapId, false)))
            .ToList();
    }

    /// <summary>Khoảng UTC tương ứng 00:00 ngày 1/1/fromYear đến 00:00 ngày 1/1/(toYear+1) giờ VN.</summary>
    private static (DateTime Start, DateTime End) Range(int fromYear, int toYear)
    {
        if (fromYear < 2000 || toYear > 2100) throw new InvalidArgumentException("Năm thống kê phải trong khoảng 2000–2100.");
        if (toYear < fromYear) throw new InvalidArgumentException("Năm kết thúc phải lớn hơn hoặc bằng năm bắt đầu.");
        if (toYear - fromYear + 1 > MaxYears) throw new InvalidArgumentException($"Chỉ thống kê tối đa {MaxYears} năm.");
        return (HotelTime.ToUtc(new DateTime(fromYear, 1, 1)), HotelTime.ToUtc(new DateTime(toYear + 1, 1, 1)));
    }

    private static string Normalize(string? value, params string[] allowed)
    {
        var v = (value ?? allowed[0]).Trim().ToLowerInvariant();
        return allowed.Contains(v) ? v : throw new InvalidArgumentException($"group_by phải là một trong: {string.Join(", ", allowed)}.");
    }

    private static string Key(DateTime local, string groupBy) => groupBy switch
    {
        "month" => $"{local.Year}-{local.Month:00}",
        "quarter" => $"{local.Year}-Q{(local.Month - 1) / 3 + 1}",
        _ => local.Year.ToString()
    };

    /// <summary>Mọi kỳ trong khoảng năm (kể cả kỳ không có hóa đơn) để biểu đồ liền mạch.</summary>
    private static IEnumerable<(string Key, string Label, int Year)> AllPeriods(int fromYear, int toYear, string groupBy)
    {
        for (var y = fromYear; y <= toYear; y++)
        {
            if (groupBy == "month") for (var m = 1; m <= 12; m++) yield return ($"{y}-{m:00}", $"{m:00}/{y}", y);
            else if (groupBy == "quarter") for (var q = 1; q <= 4; q++) yield return ($"{y}-Q{q}", $"Q{q}/{y}", y);
            else yield return (y.ToString(), y.ToString(), y);
        }
    }
}
