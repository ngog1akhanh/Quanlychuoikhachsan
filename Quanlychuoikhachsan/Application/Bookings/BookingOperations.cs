using System.Data;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed partial class BookingService
{
    private async Task LockBookingAsync(long id, CancellationToken ct)
    {
        if (dbContext.Database.IsRelational()) await dbContext.DatPhongs.FromSqlInterpolated($"SELECT * FROM dat_phong WHERE id = {id} FOR UPDATE").ToListAsync(ct);
    }
    public async Task<InvoiceResponse> InvoiceResponseAsync(HoaDonPhong invoice, CancellationToken ct)
    {
        var deposit = await dbContext.ApDungCocHoaDons.Where(x => x.HoaDonPhongId == invoice.Id).SumAsync(x => (decimal?)x.SoTienApDung, ct) ?? 0;
        var paid = await dbContext.ThanhToanHoaDonPhongs.Where(x => x.HoaDonPhongId == invoice.Id && x.TrangThai == "THANH_CONG").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
        return new(invoice.Id, $"invoices/{invoice.Id}", invoice.DatPhongId, invoice.TienPhong, invoice.TienPhuThu, invoice.TienBoiThuong, invoice.TienThue, invoice.TongTien, deposit, paid, Math.Max(0, invoice.TongTien - deposit - paid), invoice.TrangThai);
    }
    public async Task<decimal> RequiredDepositAsync(DatPhong booking, CancellationToken ct)
    {
        var policy = booking.ChinhSachCocId.HasValue ? await dbContext.Set<ChinhSachCoc>().SingleOrDefaultAsync(x => x.Id == booking.ChinhSachCocId, ct) : null;
        if (policy is null) return Input.Round(booking.TongTienDuKien * 0.3m);
        var first = HotelTime.StayDate(booking.DuKienNhan);
        var firstNight = await dbContext.GiaDemDatPhongs.Where(x => x.NgayLuuTru == first && dbContext.ChiTietDatPhongs.Any(d => d.Id == x.ChiTietDatPhongId && d.DatPhongId == booking.Id)).SumAsync(x => (decimal?)x.GiaSauDieuChinh, ct) ?? 0;
        return Math.Min(booking.TongTienDuKien, Input.Round(policy.KieuTinh switch { "PHAN_TRAM" => booking.TongTienDuKien * policy.GiaTri / 100, "DEM_DAU" => firstNight, "SO_TIEN_CO_DINH" => policy.GiaTri, _ => throw new InvalidArgumentException("Unknown deposit policy type.") }));
    }
    public async Task<decimal> ExpectedRefundAsync(DatPhong booking, CancellationToken ct)
    {
        var total = await dbContext.PhieuCocs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_THANH_TOAN").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
        var hours = (booking.DuKienNhan - DateTime.UtcNow).TotalHours;
        var rule = await dbContext.Set<ChiTietChinhSachHuy>().Where(x => x.ChinhSachHuyId == booking.ChinhSachHuyId && x.SoGioTruocToiThieu <= hours).OrderByDescending(x => x.SoGioTruocToiThieu).FirstOrDefaultAsync(ct);
        return rule is null ? 0 : Math.Max(0, Math.Min(total, Input.Round(total * rule.PhanTramHoan / 100 - rule.PhiHuyCoDinh)));
    }
    public async Task<InvoiceResponse> PrepareInvoiceAsync(long bookingId, CheckOutRequest request, CancellationToken ct)
    {
        var employee = await actor.EmployeeIdAsync(ct);
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "DANG_O") throw new InvalidArgumentException("Only an active stay can be invoiced.");
        var automatic = await SurchargeAsync(booking, ct);
        var invoice = await CreateInvoiceAsync(booking, employee, automatic + request.EarlyOrLateSurcharge, request.VatRate, ct);
        var response = await InvoiceResponseAsync(invoice, ct);
        invoice.TrangThai = response.Due == 0 ? "DA_THANH_TOAN" : response.DepositApplied > 0 ? "TT_MOT_PHAN" : "DA_PHAT_HANH";
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await InvoiceResponseAsync(invoice, ct);
    }
    private async Task<decimal> SurchargeAsync(DatPhong booking, CancellationToken ct)
    {
        var parts = await SurchargePartsAsync(booking, ct);
        return parts.Early + parts.Late;
    }
    private async Task<(decimal Early, decimal Late)> SurchargePartsAsync(DatPhong booking, CancellationToken ct)
    {
        var rules = await dbContext.Set<ChinhSachPhuThu>().Where(x => x.TrangThai == "HOAT_DONG").ToListAsync(ct);
        var arrived = booking.ChiTietDatPhongs.Min(x => x.NhanPhongLuc) ?? booking.DuKienNhan;
        var early = (booking.DuKienNhan - arrived).TotalMinutes; var late = (DateTime.UtcNow - booking.DuKienTra).TotalMinutes;
        var ids = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var nights = await dbContext.GiaDemDatPhongs.Where(x => ids.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        if (nights.Count == 0) throw new InvalidArgumentException("Booking has no nightly prices.");
        decimal earlyAmount = 0, lateAmount = 0;
        foreach (var (kind, minutes, date) in new[] { ("NHAN_SOM", early, nights.Min(x => x.NgayLuuTru)), ("TRA_TRE", late, nights.Max(x => x.NgayLuuTru)) })
        {
            if (minutes <= 0) continue;
            var rule = rules.Where(x => x.Loai == kind && x.TuPhut <= minutes && (!x.DenPhut.HasValue || x.DenPhut >= minutes)).OrderByDescending(x => x.TuPhut).ThenByDescending(x => x.Id).FirstOrDefault();
            if (rule is not null)
            {
                var amount = rule.KieuTinh == "PHAN_TRAM" ? nights.Where(x => x.NgayLuuTru == date).Sum(x => x.GiaSauDieuChinh) * rule.GiaTri / 100 : rule.GiaTri;
                if (kind == "NHAN_SOM") earlyAmount = amount; else lateAmount = amount;
            }
        }
        return (Input.Round(earlyAmount), Input.Round(lateAmount));
    }

    public async Task<BookingResponse> RescheduleAsync(long id, DateTime checkIn, DateTime checkOut, string reason, CancellationToken ct)
    {
        Input.Text(reason, 1000, "reason");
        checkIn = HotelTime.ToUtc(checkIn); checkOut = HotelTime.ToUtc(checkOut);
        var from = HotelTime.StayDate(checkIn); var to = HotelTime.StayDate(checkOut);
        if (to <= from || to.DayNumber - from.DayNumber > 365 || checkOut <= DateTime.UtcNow) throw new InvalidArgumentException("Invalid new stay dates.");
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(id, ct); var booking = await LoadBookingForBillingAsync(id, ct);
        if (booking.TrangThai is not ("HOLD" or "BOOKED" or "DANG_O")) throw new InvalidArgumentException("Booking cannot be rescheduled.");
        if (await dbContext.Set<DepositPaymentOrder>().AnyAsync(x => x.BookingId == id && x.Status == "PENDING", ct)) throw new InvalidArgumentException("Reconcile the pending deposit payment before changing dates.");
        if (booking.TrangThai == "DANG_O" && checkIn.ToUniversalTime() != booking.DuKienNhan) throw new InvalidArgumentException("Arrival cannot change after check-in.");
        if (booking.TrangThai != "DANG_O" && checkIn < DateTime.UtcNow) throw new InvalidArgumentException("Arrival cannot be in the past.");
        if (await dbContext.HoaDonPhongs.AnyAsync(x => x.DatPhongId == id && x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY", ct)) throw new InvalidArgumentException("Void the issued invoice before changing stay dates.");
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var locks = await dbContext.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        if (booking.TrangThai == "HOLD" && (locks.Count == 0 || locks.Any(x => x.HetHanGiuCho <= DateTime.UtcNow))) throw new InvalidArgumentException("Booking hold has expired.");
        var expiry = locks.Select(x => x.HetHanGiuCho).FirstOrDefault(x => x.HasValue);
        dbContext.KhoaLichPhongs.RemoveRange(locks.Where(x => x.NgayLuuTru < from || x.NgayLuuTru >= to));
        var oldPrices = await dbContext.GiaDemDatPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        dbContext.GiaDemDatPhongs.RemoveRange(oldPrices.Where(x => x.NgayLuuTru < from || x.NgayLuuTru >= to));
        foreach (var detail in booking.ChiTietDatPhongs)
        {
            var room = await dbContext.Phongs.Include(x => x.LoaiPhong).SingleAsync(x => x.Id == detail.PhongId, ct);
            if (room.TrangThai is "BAO_TRI" or "NGUNG_SU_DUNG") throw new InvalidArgumentException("Room is not available.");
            var newPrices = await CalculateNightsAsync(room, from, to, ct);
            foreach (var price in newPrices)
            {
                if (!locks.Any(x => x.ChiTietDatPhongId == detail.Id && x.NgayLuuTru == price.StayDate))
                    dbContext.Add(new KhoaLichPhong { ChiTietDatPhongId = detail.Id, PhongId = room.Id, NgayLuuTru = price.StayDate, TrangThai = booking.TrangThai == "DANG_O" ? "OCCUPIED" : booking.TrangThai, HetHanGiuCho = expiry, CreatedAt = DateTime.UtcNow });
                if (!oldPrices.Any(x => x.ChiTietDatPhongId == detail.Id && x.NgayLuuTru == price.StayDate)) dbContext.Add(ToNight(detail.Id, price));
            }
        }
        booking.DuKienNhan = checkIn.ToUniversalTime(); booking.DuKienTra = checkOut.ToUniversalTime(); booking.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct); await RecalculateBookingAsync(booking, ct);
        await notifications.BookingAsync(booking, actor.AccountId, booking.TrangThai, reason, ct); await tx.CommitAsync(ct); return await LoadBookingResponseAsync(id, ct);
    }

    public async Task<BookingResponse> MoveRoomAsync(long bookingId, long detailId, long newRoomId, string reason, CancellationToken ct)
    {
        Input.Text(reason, 1000, "reason"); var employee = await actor.EmployeeIdAsync(ct);
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct); var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai is not ("BOOKED" or "DANG_O")) throw new InvalidArgumentException("Only confirmed or active stays can move rooms.");
        var detail = booking.ChiTietDatPhongs.SingleOrDefault(x => x.Id == detailId) ?? throw new ResourceNotFoundException("Booking detail was not found.");
        if (booking.ChiTietDatPhongs.Any(x => x.PhongId == newRoomId)) throw new InvalidArgumentException("Room already belongs to this booking.");
        if (await dbContext.HoaDonPhongs.AnyAsync(x => x.DatPhongId == bookingId && x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY", ct)) throw new InvalidArgumentException("Void the issued invoice before moving rooms.");
        var room = await dbContext.Phongs.Include(x => x.LoaiPhong).SingleOrDefaultAsync(x => x.Id == newRoomId && x.LoaiPhong.TrangThai == "HOAT_DONG", ct) ?? throw new ResourceNotFoundException("New room was not found.");
        if (room.TrangThai is "BAO_TRI" or "NGUNG_SU_DUNG" || (booking.TrangThai == "DANG_O" && room.TrangThai != "SAN_SANG") || room.LoaiPhong.SoNguoiLonToiDa < detail.SoNguoiLon || room.LoaiPhong.SoTreEmToiDa < detail.SoTreEm) throw new InvalidArgumentException("New room is unavailable or too small.");
        var from = HotelTime.StayDate(booking.TrangThai == "DANG_O" ? DateTime.UtcNow : booking.DuKienNhan); var to = HotelTime.StayDate(booking.DuKienTra);
        if (from >= to) throw new InvalidArgumentException("No remaining nights to move.");
        var locks = await dbContext.KhoaLichPhongs.Where(x => x.ChiTietDatPhongId == detailId && x.NgayLuuTru >= from).ToListAsync(ct); dbContext.RemoveRange(locks);
        var prices = await dbContext.GiaDemDatPhongs.Where(x => x.ChiTietDatPhongId == detailId && x.NgayLuuTru >= from).ToListAsync(ct); dbContext.RemoveRange(prices);
        await dbContext.SaveChangesAsync(ct);
        foreach (var price in await CalculateNightsAsync(room, from, to, ct))
        {
            dbContext.Add(ToNight(detailId, price));
            dbContext.Add(new KhoaLichPhong { ChiTietDatPhongId = detailId, PhongId = room.Id, NgayLuuTru = price.StayDate, TrangThai = booking.TrangThai == "DANG_O" ? "OCCUPIED" : "BOOKED", CreatedAt = DateTime.UtcNow });
        }
        dbContext.Add(new LichSuDoiPhong { DatPhongId = bookingId, ChiTietDatPhongId = detailId, PhongCuId = detail.PhongId, PhongMoiId = room.Id, NhanVienId = employee, LyDo = reason, DoiLuc = DateTime.UtcNow });
        if (booking.TrangThai == "DANG_O")
        {
            var oldRoom = await dbContext.Phongs.SingleAsync(x => x.Id == detail.PhongId, ct); oldRoom.TrangThai = "CAN_DON"; room.TrangThai = "DANG_O";
            dbContext.Add(new CongViecBuongPhong { MaCongViec = $"HK{Guid.NewGuid():N}", PhongId = oldRoom.Id, DatPhongId = bookingId, LoaiCongViec = "DON_CHECKOUT", NhanVienPhanCongId = employee });
        }
        detail.PhongId = room.Id; booking.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct); await RecalculateBookingAsync(booking, ct); await notifications.BookingAsync(booking, actor.AccountId, booking.TrangThai, reason, ct);
        await tx.CommitAsync(ct); return await LoadBookingResponseAsync(bookingId, ct);
    }
    private static GiaDemDatPhong ToNight(long detailId, NightPriceResponse p) => new() { ChiTietDatPhongId = detailId, NgayLuuTru = p.StayDate, GiaCoBan = p.BasePrice, PhuThuPhong = p.RoomSurcharge, ChinhSachGiaId = p.PricingPolicyId, TienDieuChinh = p.Adjustment, GiaSauDieuChinh = p.FinalPrice };
    private async Task RecalculateBookingAsync(DatPhong booking, CancellationToken ct)
    {
        var ids = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray(); var prices = await dbContext.GiaDemDatPhongs.Where(x => ids.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        booking.TongTienDuKien = prices.Sum(x => x.GiaSauDieuChinh); booking.TongTienPhongGoc = prices.Sum(x => x.GiaCoBan);
        foreach (var d in booking.ChiTietDatPhongs) d.TienPhongDuKien = prices.Where(x => x.ChiTietDatPhongId == d.Id).Sum(x => x.GiaSauDieuChinh);
        await dbContext.SaveChangesAsync(ct);
    }
}
