using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.FrontDesk;

/// <summary>Chuyển hợp đồng WinForms sang dịch vụ booking dùng chung.</summary>
public sealed partial class FrontDeskService(HotelDbContext db, BookingService bookings, CurrentActor actor) : IFrontDeskService
{
    public async Task<FrontDeskBookingPage> SearchAsync(FrontDeskBookingQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var q = db.DatPhongs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            q = q.Where(x => x.TrangThai == status);
        }
        if (query.From is { } fromDate)
        {
            var fromDt = HotelTime.ToUtc(fromDate.ToDateTime(TimeOnly.MinValue));
            q = q.Where(x => x.DuKienTra >= fromDt);
        }
        if (query.To is { } toDate)
        {
            var toDt = HotelTime.ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
            q = q.Where(x => x.DuKienNhan < toDt);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            q = q.Where(x => x.MaDatPhong.Contains(text)
                || x.KhachHang.HoTen.Contains(text)
                || (x.KhachHang.SoDienThoai != null && x.KhachHang.SoDienThoai.Contains(text))
                || x.ChiTietDatPhongs.Any(d => d.Phong.SoPhong == text));
        }

        var total = await q.CountAsync(ct);
        var paged = q.OrderByDescending(x => x.DuKienNhan).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size);
        var items = await ProjectAsync(paged, ct);
        return new FrontDeskBookingPage(items, total, page, size);
    }

    public async Task<FrontDeskBookingDetail> GetDetailAsync(long bookingId, CancellationToken ct)
    {
        var summary = (await ProjectAsync(db.DatPhongs.AsNoTracking().Where(x => x.Id == bookingId), ct)).SingleOrDefault()
            ?? throw new ResourceNotFoundException($"Không tìm thấy booking #{bookingId}.");

        var booking = await db.DatPhongs.AsNoTracking()
            .Include(x => x.KhachHang)
            .Include(x => x.ChiTietDatPhongs).ThenInclude(d => d.Phong).ThenInclude(p => p.LoaiPhong)
            .SingleAsync(x => x.Id == bookingId, ct);

        var c = booking.KhachHang;
        var stays = await db.DatPhongs.CountAsync(x => x.KhachHangId == c.Id && x.TrangThai == "DA_TRA", ct);
        var customer = new FrontDeskCustomer(c.Id, c.MaKhachHang, c.HoTen, c.SoDienThoai, c.Email, c.CccdHoChieu, c.DiaChi, stays);

        var details = booking.ChiTietDatPhongs.OrderBy(d => d.Phong.SoPhong).ToList();
        var detailIds = details.Select(d => d.Id).ToArray();
        var roomNumberByDetail = details.ToDictionary(d => d.Id, d => d.Phong.SoPhong);

        var rooms = details.Select(d => new BookingRoomLine(d.Id, d.PhongId, d.Phong.SoPhong, d.Phong.LoaiPhong.TenLoaiPhong,
            d.Phong.TrangThai, d.TrangThai, d.TienPhongDuKien, HotelTime.Local(d.NhanPhongLuc), HotelTime.Local(d.TraPhongLuc))).ToList();

        var nightRows = await db.GiaDemDatPhongs.AsNoTracking()
            .Where(x => detailIds.Contains(x.ChiTietDatPhongId))
            .OrderBy(x => x.NgayLuuTru).ToListAsync(ct);
        var nights = nightRows.Select(n => new BookingNightLine(n.ChiTietDatPhongId, roomNumberByDetail.GetValueOrDefault(n.ChiTietDatPhongId, "?"),
            n.NgayLuuTru, n.GiaCoBan, n.PhuThuPhong, n.TienDieuChinh, n.GiaSauDieuChinh)).ToList();

        var guests = await db.Set<KhachLuuTru>().AsNoTracking()
            .Where(x => detailIds.Contains(x.ChiTietDatPhongId))
            .OrderBy(x => x.ChiTietDatPhongId).ThenByDescending(x => x.LaKhachDaiDien)
            .Select(x => new StayGuestDto(x.Id, x.ChiTietDatPhongId, x.HoTen, x.NgaySinh, x.GioiTinh, x.LoaiGiayTo, x.SoGiayTo, x.QuocTich, x.LaKhachDaiDien))
            .ToListAsync(ct);

        var methods = await db.PhuongThucThanhToans.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.TenPhuongThuc, ct);
        var deposits = (await db.PhieuCocs.AsNoTracking().Where(x => x.DatPhongId == bookingId).OrderBy(x => x.ThoiGianCoc).ToListAsync(ct))
            .Select(x => new DepositLine(x.Id, x.MaPhieuCoc, x.SoTien, methods.GetValueOrDefault(x.PhuongThucId, "?"), HotelTime.Local(x.ThoiGianCoc), x.TrangThai))
            .ToList();

        var invoices = await BuildInvoiceLinesAsync(bookingId, ct);

        var cancellations = await db.YeuCauHuyDatPhongs.AsNoTracking().Where(x => x.DatPhongId == bookingId)
            .OrderByDescending(x => x.ThoiGianYeuCau)
            .Select(x => new CancellationLine(x.Id, x.MaYeuCau, x.LyDo, HotelTime.Local(x.ThoiGianYeuCau), x.SoTienHoanDuKien, x.SoTienHoanDuyet, x.TrangThai, x.GhiChuXuLy))
            .ToListAsync(ct);

        var historyRows = await db.Set<LichSuTrangThaiDatPhong>().AsNoTracking()
            .Where(x => x.DatPhongId == bookingId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var accountIds = historyRows.Where(x => x.TaiKhoanThucHienId != null).Select(x => x.TaiKhoanThucHienId!.Value).Distinct().ToArray();
        var accountNames = await db.TaiKhoans.AsNoTracking().Where(x => accountIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.NhanVien != null ? x.NhanVien.HoTen : (x.Email ?? x.SoDienThoai) })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var history = historyRows.Select(h => new StatusHistoryLine(HotelTime.Local(h.CreatedAt), h.TrangThaiCu, h.TrangThaiMoi,
            h.TaiKhoanThucHienId is { } aid ? accountNames.GetValueOrDefault(aid) : null, h.LyDo)).ToList();

        var holdExpires = await db.KhoaLichPhongs.AsNoTracking()
            .Where(x => detailIds.Contains(x.ChiTietDatPhongId) && x.TrangThai == "HOLD")
            .Select(x => x.HetHanGiuCho).MaxAsync(ct);

        return new FrontDeskBookingDetail(summary, customer, booking.YeuCauDacBiet, HotelTime.Local(holdExpires), rooms, nights, guests,
            deposits, invoices, cancellations, history);
    }

    public async Task<IReadOnlyList<AvailableRoom>> FindAvailableRoomsAsync(DateOnly checkIn, DateOnly checkOut, long? excludeBookingId, CancellationToken ct)
    {
        if (checkOut <= checkIn) throw new InvalidArgumentException("Ngày trả phòng phải sau ngày nhận phòng.");
        if (checkOut.DayNumber - checkIn.DayNumber > 90) throw new InvalidArgumentException("Chỉ tra cứu tối đa 90 đêm.");
        var now = DateTime.UtcNow;

        var busy = db.KhoaLichPhongs.Where(x => x.NgayLuuTru >= checkIn && x.NgayLuuTru < checkOut &&
            (x.TrangThai == "BOOKED" || x.TrangThai == "OCCUPIED" || (x.TrangThai == "HOLD" && x.HetHanGiuCho > now)));
        if (excludeBookingId is { } exclude)
        {
            var ownDetails = db.ChiTietDatPhongs.Where(d => d.DatPhongId == exclude).Select(d => d.Id);
            busy = busy.Where(x => !ownDetails.Contains(x.ChiTietDatPhongId));
        }
        var busyRoomIds = busy.Select(x => x.PhongId);

        return await db.Phongs.AsNoTracking()
            .Where(x => x.TrangThai != "BAO_TRI" && x.TrangThai != "NGUNG_SU_DUNG" && x.LoaiPhong.TrangThai == "HOAT_DONG")
            .Where(x => !busyRoomIds.Contains(x.Id))
            .OrderBy(x => x.SoPhong)
            .Select(x => new AvailableRoom(x.Id, x.SoPhong, x.Tang, x.LoaiView, x.LoaiPhongId, x.LoaiPhong.TenLoaiPhong,
                x.LoaiPhong.SoNguoiLonToiDa, x.LoaiPhong.SoTreEmToiDa, x.LoaiPhong.GiaCoBanDem, x.PhuThuDemMacDinh, x.TrangThai))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FrontDeskCustomer>> FindCustomersAsync(string? q, CancellationToken ct)
    {
        var query = db.KhachHangs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(x => x.HoTen.Contains(text)
                || (x.SoDienThoai != null && x.SoDienThoai.Contains(text))
                || (x.Email != null && x.Email.Contains(text))
                || (x.CccdHoChieu != null && x.CccdHoChieu.Contains(text)));
        }
        return await query.OrderBy(x => x.HoTen).Take(30)
            .Select(x => new FrontDeskCustomer(x.Id, x.MaKhachHang, x.HoTen, x.SoDienThoai, x.Email, x.CccdHoChieu, x.DiaChi,
                x.DatPhongs.Count(d => d.TrangThai == "DA_TRA")))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentMethodItem>> ListPaymentMethodsAsync(CancellationToken ct)
        => await db.PhuongThucThanhToans.AsNoTracking()
            .Where(x => x.TrangThai == "HOAT_DONG" && (x.MaPhuongThuc == "TIEN_MAT" || x.MaPhuongThuc == "CHUYEN_KHOAN" || x.MaPhuongThuc == "THE" || x.MaPhuongThuc == "CASH" || x.MaPhuongThuc == "BANK_TRANSFER" || x.MaPhuongThuc == "CARD"))
            .OrderBy(x => x.Id)
            .Select(x => new PaymentMethodItem(x.Id, x.MaPhuongThuc, x.TenPhuongThuc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CancellationRequestItem>> ListCancellationsAsync(string? status, CancellationToken ct)
    {
        var q = from r in db.YeuCauHuyDatPhongs.AsNoTracking()
                join b in db.DatPhongs on r.DatPhongId equals b.Id
                select new { r, b.MaDatPhong, b.KhachHang.HoTen };
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.r.TrangThai == status);
        var rows = await q.OrderByDescending(x => x.r.ThoiGianYeuCau).Take(200).ToListAsync(ct);
        return rows.Select(x => ToItem(x.r, x.MaDatPhong, x.HoTen)).ToList();
    }

    public async Task<FrontDeskDay> GetDayAsync(DateOnly date, CancellationToken ct)
    {
        var start = HotelTime.ToUtc(date.ToDateTime(TimeOnly.MinValue));
        var end = start.AddDays(1);
        var arrivals = await ProjectAsync(db.DatPhongs.AsNoTracking()
            .Where(x => x.DuKienNhan >= start && x.DuKienNhan < end &&
                (x.TrangThai == "HOLD" || x.TrangThai == "CHO_COC" || x.TrangThai == "BOOKED" || x.TrangThai == "CHO_DUYET_HUY" || x.TrangThai == "DANG_O"))
            .OrderBy(x => x.DuKienNhan), ct);
        var departures = await ProjectAsync(db.DatPhongs.AsNoTracking()
            .Where(x => x.DuKienTra >= start && x.DuKienTra < end && (x.TrangThai == "DANG_O" || x.TrangThai == "DA_TRA"))
            .OrderBy(x => x.DuKienTra), ct);
        var inHouse = await ProjectAsync(db.DatPhongs.AsNoTracking()
            .Where(x => x.TrangThai == "DANG_O")
            .OrderBy(x => x.DuKienTra), ct);
        return new FrontDeskDay(date, arrivals, departures, inHouse);
    }

    private async Task<List<FrontDeskBookingItem>> ProjectAsync(IQueryable<DatPhong> source, CancellationToken ct)
    {
        var rows = await source.Select(x => new
        {
            x.Id,
            x.MaDatPhong,
            x.TrangThai,
            x.KenhDat,
            x.KhachHangId,
            CustomerName = x.KhachHang.HoTen,
            CustomerPhone = x.KhachHang.SoDienThoai,
            Rooms = x.ChiTietDatPhongs.OrderBy(d => d.Phong.SoPhong).Select(d => d.Phong.SoPhong).ToList(),
            x.DuKienNhan,
            x.DuKienTra,
            x.SoNguoiLon,
            x.SoTreEm,
            x.TongTienDuKien,
            Deposit = db.PhieuCocs.Where(p => p.DatPhongId == x.Id && p.TrangThai == "DA_THANH_TOAN").Sum(p => (decimal?)p.SoTien) ?? 0,
            x.NgayDat
        }).ToListAsync(ct);

        return rows.Select(x => new FrontDeskBookingItem(x.Id, x.MaDatPhong, x.TrangThai, x.KenhDat, x.KhachHangId, x.CustomerName,
            x.CustomerPhone, string.Join(", ", x.Rooms), HotelTime.Local(x.DuKienNhan), HotelTime.Local(x.DuKienTra),
            Math.Max(1, HotelTime.StayDate(x.DuKienTra).DayNumber - HotelTime.StayDate(x.DuKienNhan).DayNumber), x.SoNguoiLon, x.SoTreEm, x.TongTienDuKien, x.Deposit, HotelTime.Local(x.NgayDat))).ToList();
    }

    private async Task<List<InvoiceLine>> BuildInvoiceLinesAsync(long bookingId, CancellationToken ct)
    {
        var invoices = await db.HoaDonPhongs.AsNoTracking().Where(x => x.DatPhongId == bookingId).OrderBy(x => x.NgayLap).ToListAsync(ct);
        var ids = invoices.Select(x => x.Id).ToArray();
        var paid = await db.ThanhToanHoaDonPhongs.AsNoTracking()
            .Where(x => ids.Contains(x.HoaDonPhongId) && x.TrangThai == "THANH_CONG")
            .GroupBy(x => x.HoaDonPhongId).Select(g => new { g.Key, Sum = g.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var applied = await db.ApDungCocHoaDons.AsNoTracking()
            .Where(x => ids.Contains(x.HoaDonPhongId))
            .GroupBy(x => x.HoaDonPhongId).Select(g => new { g.Key, Sum = g.Sum(x => x.SoTienApDung) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        return invoices.Select(x =>
        {
            var p = paid.GetValueOrDefault(x.Id);
            var a = applied.GetValueOrDefault(x.Id);
            var due = x.TrangThai == "DA_HUY" ? 0 : Math.Max(0, x.TongTien - p - a);
            return new InvoiceLine(x.Id, x.MaHoaDon, HotelTime.Local(x.NgayLap), x.TienPhong, x.TienPhuThu, x.TienBoiThuong, x.TienThue, x.TongTien, a, p, due, x.TrangThai);
        }).ToList();
    }

    private async Task<long> ResolveCustomerAsync(CreateDeskBookingRequest request, DateTime now, CancellationToken ct)
    {
        if (request.CustomerId is { } id)
        {
            if (!await db.KhachHangs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Không tìm thấy khách hàng.");
            return id;
        }
        var n = request.NewCustomer ?? throw new InvalidArgumentException("Chọn khách hàng hoặc nhập khách mới.");
        if (string.IsNullOrWhiteSpace(n.FullName)) throw new InvalidArgumentException("Họ tên khách không được trống.");
        var customer = new KhachHang
        {
            MaKhachHang = $"KH{Guid.NewGuid():N}"[..30],
            HoTen = Input.Text(n.FullName, 120, "full_name"),
            SoDienThoai = Clean(n.Phone),
            Email = Clean(n.Email)?.ToLowerInvariant(),
            CccdHoChieu = Clean(n.IdentityNumber),
            DiaChi = Clean(n.Address),
            GhiChu = "Khách tạo tại quầy",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.KhachHangs.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer.Id;
    }
    private static CancellationRequestItem ToItem(YeuCauHuyDatPhong r, string code, string customer)
        => new(r.Id, r.MaYeuCau, r.DatPhongId, code, customer, r.LyDo, HotelTime.Local(r.ThoiGianYeuCau), r.SoTienHoanDuKien, r.SoTienHoanDuyet, r.TrangThai, r.GhiChuXuLy);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
