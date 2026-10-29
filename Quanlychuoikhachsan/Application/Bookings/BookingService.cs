using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed partial class BookingService(HotelDbContext dbContext, CurrentActor actor, NotificationWriter notifications) : IBookingService
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(15);
    public async Task<IReadOnlyList<RoomSummaryResponse>> FindAvailableRoomsAsync(RoomAvailabilityQuery query, CancellationToken cancellationToken)
    {
        ValidateDates(query.CheckInDate, query.CheckOutDate);
        var occupiedRoomIds = dbContext.KhoaLichPhongs
            .Where(x => x.NgayLuuTru >= query.CheckInDate && x.NgayLuuTru < query.CheckOutDate &&
                (x.TrangThai == "BOOKED" || x.TrangThai == "OCCUPIED" || (x.TrangThai == "HOLD" && x.HetHanGiuCho > DateTime.UtcNow)))
            .Select(x => x.PhongId);

        return await dbContext.Phongs.AsNoTracking()
            .Where(x => x.TrangThai != "BAO_TRI" && x.TrangThai != "NGUNG_SU_DUNG" && x.LoaiPhong.TrangThai == "HOAT_DONG" && !occupiedRoomIds.Contains(x.Id))
            .Where(x => x.LoaiPhong.SoNguoiLonToiDa >= query.Adults && x.LoaiPhong.SoTreEmToiDa >= query.Children)
            .Select(x => new RoomSummaryResponse(x.Id, x.MaPhong, x.SoPhong, x.LoaiPhong.MaLoaiPhong, x.LoaiPhong.TenLoaiPhong, x.LoaiPhong.GiaCoBanDem))
            .ToListAsync(cancellationToken);
    }

    public async Task<BookingResponse> UpdateBookingAsync(long accountId, long bookingId, PatchBookingRequest request, CancellationToken ct)
    {
        if (request.SpecialRequest?.Length > 1000)
            throw new InvalidArgumentException("special_request cannot exceed 1000 characters.");

        var booking = await dbContext.DatPhongs.SingleOrDefaultAsync(
            x => x.Id == bookingId && x.KhachHang.TaiKhoanId == accountId, ct)
            ?? throw new ResourceNotFoundException($"Booking bookings/{bookingId} was not found.");

        if (booking.TrangThai is not ("HOLD" or "BOOKED"))
            throw new InvalidArgumentException("Special requests can only be updated before check-in.");

        booking.YeuCauDacBiet = request.SpecialRequest;
        booking.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return await LoadBookingResponseAsync(bookingId, ct);
    }
    public async Task<InvoiceResponse> CheckInAsync(long bookingId, CheckInRequest request, CancellationToken ct)
    {
        var employee = await actor.EmployeeIdAsync(ct);
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "BOOKED") throw new InvalidArgumentException("Only confirmed bookings can check in.");
        if (HotelTime.StayDate(booking.DuKienNhan) > HotelTime.Today) throw new InvalidArgumentException("Arrival date has not started.");
        if (DateTime.UtcNow >= booking.DuKienTra) throw new InvalidArgumentException("The scheduled stay has ended.");
        var ids = booking.ChiTietDatPhongs.Select(x => x.PhongId).ToArray();
        var rooms = await dbContext.Phongs.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (rooms.Count == 0 || rooms.Any(x => x.TrangThai != "SAN_SANG")) throw new InvalidArgumentException("All rooms must be ready before check-in.");
        foreach (var d in booking.ChiTietDatPhongs) { d.NhanPhongLuc = DateTime.UtcNow; d.TrangThai = "DANG_O"; }
        foreach (var room in rooms) room.TrangThai = "DANG_O";
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        foreach (var l in await dbContext.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct)) l.TrangThai = "OCCUPIED";
        booking.TrangThai = "DANG_O"; booking.UpdatedAt = DateTime.UtcNow;
        var invoice = await CreateInvoiceAsync(booking, employee, 0, 0, ct);
        invoice.TrangThai = "NHAP";
        await notifications.BookingAsync(booking, actor.AccountId, "BOOKED", "Khách đã nhận phòng.", ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await InvoiceResponseAsync(invoice, ct);
    }
    public async Task<InvoiceResponse> CheckOutAsync(long bookingId, CheckOutRequest request, CancellationToken ct)
    {
        var employee = await actor.EmployeeIdAsync(ct);
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "DANG_O") throw new InvalidArgumentException("Only an active stay can check out.");
        var invoice = await dbContext.HoaDonPhongs.SingleOrDefaultAsync(x => x.DatPhongId == bookingId && x.TrangThai != "DA_HUY", ct)
            ?? throw new InvalidArgumentException("Prepare the room invoice before checkout.");
        var response = await InvoiceResponseAsync(invoice, ct);
        if (invoice.TrangThai == "NHAP" || response.Due > 0) throw new InvalidArgumentException("Issue and settle the invoice before checkout using /bookings/{id}/invoice:prepare and /invoices/{id}:pay.");
        if (request.EarlyOrLateSurcharge != 0) throw new InvalidArgumentException("Set surcharges when preparing the invoice.");
        foreach (var detail in booking.ChiTietDatPhongs)
        {
            detail.TraPhongLuc = DateTime.UtcNow; detail.TrangThai = "DA_TRA";
            var room = await dbContext.Phongs.SingleAsync(x => x.Id == detail.PhongId, ct); room.TrangThai = "CAN_DON";
            dbContext.CongViecBuongPhongs.Add(new CongViecBuongPhong { MaCongViec = $"HK{Guid.NewGuid():N}", PhongId = room.Id, DatPhongId = bookingId, LoaiCongViec = "DON_CHECKOUT", NhanVienPhanCongId = employee, MucDoUuTien = "CAO", TrangThai = "CHO_XU_LY" });
        }
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        dbContext.KhoaLichPhongs.RemoveRange(await dbContext.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId) && x.NgayLuuTru >= HotelTime.Today).ToListAsync(ct));
        booking.TrangThai = "DA_TRA"; booking.UpdatedAt = DateTime.UtcNow;
        await notifications.BookingAsync(booking, actor.AccountId, "DANG_O", "Khách đã trả phòng.", ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return response;
    }
    public async Task<InvoiceResponse> AddInvoicePaymentAsync(long invoiceId, AddInvoicePaymentRequest request, CancellationToken ct)
    {
        Input.Money(request.Amount, "amount"); Input.Text(request.TransactionId, 150, "transaction_id");
        var employee = await actor.EmployeeIdAsync(ct);
        if (request.Amount <= 0) throw new InvalidArgumentException("Payment amount must be positive.");
        if (string.Equals(request.PaymentGateway?.Trim(), "VietQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR payments can only be recorded after a verified inquiry.");
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        if (dbContext.Database.IsRelational()) await dbContext.HoaDonPhongs.FromSqlInterpolated($"SELECT * FROM hoa_don_phong WHERE id = {invoiceId} FOR UPDATE").ToListAsync(ct);
        var invoice = await dbContext.HoaDonPhongs.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw new ResourceNotFoundException("Room invoice was not found.");
        var existing = await dbContext.ThanhToanHoaDonPhongs.SingleOrDefaultAsync(x => x.HoaDonPhongId == invoiceId && x.MaGiaoDich == request.TransactionId, ct);
        if (existing is not null)
        {
            if (existing.SoTien != request.Amount || existing.PhuongThucId != request.PaymentMethodId) throw new AlreadyExistsException("Payment reference was used with different data.");
            return await InvoiceResponseAsync(invoice, ct);
        }
        if (invoice.TrangThai is not ("DA_PHAT_HANH" or "TT_MOT_PHAN")) throw new InvalidArgumentException("Only issued, unpaid invoices accept payments.");
        if (request.ShiftId.HasValue && !await dbContext.CaLamViecs.AnyAsync(x => x.Id == request.ShiftId && x.NhanVienId == employee && x.TrangThai == "DANG_MO", ct)) throw new InvalidArgumentException("Shift must belong to the cashier and be open.");
        if (await dbContext.YeuCauThanhToanVietQrs.AnyAsync(
            x => x.HoaDonPhongId == invoiceId && x.TrangThai != "PAID" && x.TrangThai != "REFUNDED", ct))
            throw new InvalidArgumentException("Resolve the existing VietQR order before recording a manual invoice payment; changing its local status does not invalidate its QR code.");
        var paymentMethod = await dbContext.PhuongThucThanhToans.SingleOrDefaultAsync(
            x => x.Id == request.PaymentMethodId && x.TrangThai == "HOAT_DONG", ct)
            ?? throw new ResourceNotFoundException("Payment method was not found.");
        if (string.Equals(paymentMethod.MaPhuongThuc, "VIETQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR payments can only be recorded after a verified inquiry.");
        Input.OneOf(paymentMethod.MaPhuongThuc, "TIEN_MAT", "CHUYEN_KHOAN", "THE", "CASH", "BANK_TRANSFER", "CARD");
        if (request.PaymentGateway is not null) Input.OneOf(request.PaymentGateway, "MANUAL", "CASH", "BANK_TRANSFER", "CARD");
        var paid = await dbContext.ThanhToanHoaDonPhongs.Where(x => x.HoaDonPhongId == invoiceId && x.TrangThai == "THANH_CONG").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
        var deposit = await dbContext.ApDungCocHoaDons.Where(x => x.HoaDonPhongId == invoiceId).SumAsync(x => (decimal?)x.SoTienApDung, ct) ?? 0;
        if (paid + deposit + request.Amount > invoice.TongTien) throw new InvalidArgumentException("Payment exceeds the remaining invoice balance.");
        dbContext.ThanhToanHoaDonPhongs.Add(new ThanhToanHoaDonPhong { MaThanhToan = $"TT{Guid.NewGuid():N}", HoaDonPhongId = invoiceId, NhanVienThuId = employee, CaLamViecId = request.ShiftId, PhuongThucId = request.PaymentMethodId, SoTien = request.Amount, ThoiGianThanhToan = DateTime.UtcNow, CongThanhToan = request.PaymentGateway, MaGiaoDich = request.TransactionId });
        invoice.TrangThai = paid + deposit + request.Amount >= invoice.TongTien ? "DA_THANH_TOAN" : "TT_MOT_PHAN";
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await InvoiceResponseAsync(invoice, ct);
    }

    public async Task<CancellationRequestResponse> CancelAsync(long accountId, long bookingId, CancelBookingRequest request, CancellationToken ct)
    {
        Input.Text(request.Reason, 1500, "reason");
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var booking = await actor.BookingAsync(bookingId, "BOOKING_WRITE", ct);
        if (booking.TrangThai is not ("HOLD" or "BOOKED")) throw new InvalidArgumentException("The booking cannot be cancelled in its current state.");
        var oldStatus = booking.TrangThai;
        var item = new YeuCauHuyDatPhong { MaYeuCau = $"YC{Guid.NewGuid():N}", DatPhongId = bookingId, KhachHangId = booking.KhachHangId, LyDo = request.Reason, ThoiGianYeuCau = DateTime.UtcNow, SoTienHoanDuKien = await ExpectedRefundAsync(booking, ct) };
        booking.TrangThai = "CHO_DUYET_HUY"; booking.UpdatedAt = DateTime.UtcNow;
        dbContext.YeuCauHuyDatPhongs.Add(item); await notifications.BookingAsync(booking, accountId, oldStatus, request.Reason, ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(item.Id, $"cancellation-requests/{item.Id}", bookingId, item.TrangThai, item.SoTienHoanDuKien, item.SoTienHoanDuyet);
    }

    public async Task<CancellationRequestResponse> ApproveCancellationAsync(long requestId, ApproveCancellationRequest request, CancellationToken ct)
    {
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        var item = await dbContext.YeuCauHuyDatPhongs.SingleOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new ResourceNotFoundException("Cancellation request was not found.");
        await LockBookingAsync(item.DatPhongId, ct);
        if (item.TrangThai != "CHO_DUYET" || request.ApprovedRefund < 0 || request.ApprovedRefund > item.SoTienHoanDuKien) throw new InvalidArgumentException("Cancellation request or refund amount is invalid.");
        var booking = await LoadBookingForBillingAsync(item.DatPhongId, ct);
        if (booking.TrangThai != "CHO_DUYET_HUY") throw new InvalidArgumentException("Booking is not awaiting cancellation.");
        booking.TrangThai = "DA_HUY"; booking.UpdatedAt = DateTime.UtcNow;
        foreach (var d in booking.ChiTietDatPhongs) d.TrangThai = "DA_HUY";
        item.TrangThai = "DA_DUYET"; item.SoTienHoanDuyet = request.ApprovedRefund; item.NhanVienXuLyId = await actor.EmployeeIdAsync(ct); item.ThoiGianXuLy = DateTime.UtcNow; item.GhiChuXuLy = request.Note;
        var remainingRefund = request.ApprovedRefund;
        foreach (var deposit in await dbContext.PhieuCocs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_THANH_TOAN").OrderBy(x => x.Id).ToListAsync(ct))
        {
            var reserved = await dbContext.Set<PhieuHoanTien>().Where(x => x.PhieuCocId == deposit.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
            var amount = Math.Min(remainingRefund, Math.Max(0, deposit.SoTien - reserved));
            if (amount > 0) dbContext.Add(new PhieuHoanTien { MaPhieuHoan = $"HT{Guid.NewGuid():N}", PhieuCocId = deposit.Id, YeuCauHuyId = item.Id, NhanVienId = item.NhanVienXuLyId, PhuongThucId = deposit.PhuongThucId, SoTien = amount, LyDo = request.Note ?? "Approved booking cancellation", TrangThai = "CHO_XU_LY", ThoiGianHoan = DateTime.UtcNow });
            remainingRefund -= amount;
        }
        if (remainingRefund > 0) throw new InvalidArgumentException("Refund exceeds available deposits.");
        dbContext.KhoaLichPhongs.RemoveRange(await dbContext.KhoaLichPhongs.Where(x => x.ChiTietDatPhongId != 0 && dbContext.ChiTietDatPhongs.Where(d => d.DatPhongId == item.DatPhongId).Select(d => d.Id).Contains(x.ChiTietDatPhongId)).ToListAsync(ct));
            await dbContext.SaveChangesAsync(ct);
        await notifications.BookingAsync(booking, actor.AccountId, "CHO_DUYET_HUY", request.Note ?? "Đã duyệt hủy đặt phòng.", ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(item.Id, $"cancellation-requests/{item.Id}", item.DatPhongId, item.TrangThai, item.SoTienHoanDuKien, item.SoTienHoanDuyet);
    }

    public async Task<BookingResponse> MarkNoShowAsync(long bookingId, MarkNoShowRequest request, CancellationToken ct)
    {
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "BOOKED" || DateTime.UtcNow < booking.DuKienNhan) throw new InvalidArgumentException("Only a confirmed booking past its arrival time can be marked no-show.");
        booking.TrangThai = "NO_SHOW"; booking.UpdatedAt = DateTime.UtcNow;
        foreach (var detail in booking.ChiTietDatPhongs) detail.TrangThai = "NO_SHOW";
        var ids = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        dbContext.KhoaLichPhongs.RemoveRange(await dbContext.KhoaLichPhongs.Where(x => ids.Contains(x.ChiTietDatPhongId)).ToListAsync(ct));
        await notifications.BookingAsync(booking, actor.AccountId, "BOOKED", request.Note ?? "Khách không đến.", ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await LoadBookingResponseAsync(bookingId, ct);
    }

    private async Task<DatPhong> LoadBookingForBillingAsync(long id, CancellationToken ct) => await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
    private async Task<HoaDonPhong> CreateInvoiceAsync(DatPhong booking, long employeeId, decimal surcharge, decimal vatRate, CancellationToken ct)
    {
        Input.Money(surcharge, "surcharge");
        if (vatRate is < 0 or > 1) throw new InvalidArgumentException("VAT rate must be between 0 and 1.");
        var invoice = await dbContext.HoaDonPhongs.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.DatPhongId == booking.Id && x.TrangThai != "DA_HUY", ct);
        if (invoice is not null && invoice.TrangThai != "NHAP") throw new InvalidArgumentException("An issued invoice is immutable; void and replace it to correct charges.");
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var nights = await dbContext.GiaDemDatPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        var conditions = await dbContext.TinhTrangPhongs.Where(x => x.DatPhongId == booking.Id && x.DoKhachGayRa).ToListAsync(ct);
        var roomAmount = nights.Sum(x => x.GiaSauDieuChinh); var compensation = conditions.Sum(x => x.ChiPhiThucTe);
        var taxable = roomAmount + surcharge + compensation;
        HoaDonPhong? replaced = null;
        if (invoice is null)
        {
            replaced = await dbContext.HoaDonPhongs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_HUY" && x.HoaDonThayTheId == null).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            invoice = new HoaDonPhong { MaHoaDon = $"HD{Guid.NewGuid():N}", DatPhongId = booking.Id, NhanVienLapId = employeeId, NgayLap = DateTime.UtcNow };
            dbContext.Add(invoice);
        }
        else { dbContext.RemoveRange(invoice.ChiTiet); invoice.ChiTiet.Clear(); }
        invoice.TienPhong = roomAmount; invoice.TienPhuThu = surcharge; invoice.TienBoiThuong = compensation; invoice.TienThue = Input.Round(taxable * vatRate); invoice.TongTien = taxable + invoice.TienThue; invoice.TrangThai = "NHAP";
        Input.Money(invoice.TongTien, "invoice_total");
        foreach (var night in nights) invoice.ChiTiet.Add(new ChiTietHoaDonPhong { ChiTietDatPhongId = night.ChiTietDatPhongId, LoaiDong = "TIEN_PHONG", NoiDung = $"Room night {night.NgayLuuTru:yyyy-MM-dd}", NgayApDung = night.NgayLuuTru, SoLuong = 1, DonGia = night.GiaSauDieuChinh, ThanhTien = night.GiaSauDieuChinh });
        foreach (var condition in conditions) invoice.ChiTiet.Add(new ChiTietHoaDonPhong { TinhTrangPhongId = condition.Id, LoaiDong = "BOI_THUONG", NoiDung = $"Room condition {condition.Id}", SoLuong = 1, DonGia = condition.ChiPhiThucTe, ThanhTien = condition.ChiPhiThucTe });
        if (surcharge > 0) invoice.ChiTiet.Add(new ChiTietHoaDonPhong { LoaiDong = "PHU_THU", NoiDung = "Early arrival / late departure", SoLuong = 1, DonGia = surcharge, ThanhTien = surcharge });
        if (invoice.TienThue > 0) invoice.ChiTiet.Add(new ChiTietHoaDonPhong { LoaiDong = "THUE", NoiDung = "VAT", SoLuong = 1, DonGia = invoice.TienThue, ThanhTien = invoice.TienThue });
        await dbContext.SaveChangesAsync(ct);
        if (replaced is not null) replaced.HoaDonThayTheId = invoice.Id;
        dbContext.ApDungCocHoaDons.RemoveRange(await dbContext.ApDungCocHoaDons.Where(x => x.HoaDonPhongId == invoice.Id).ToListAsync(ct)); await dbContext.SaveChangesAsync(ct);
        var remaining = invoice.TongTien;
        foreach (var deposit in await dbContext.PhieuCocs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_THANH_TOAN").OrderBy(x => x.Id).ToListAsync(ct))
        {
            var used = await dbContext.ApDungCocHoaDons.Where(x => x.PhieuCocId == deposit.Id && dbContext.HoaDonPhongs.Any(i => i.Id == x.HoaDonPhongId && i.TrangThai != "DA_HUY")).SumAsync(x => (decimal?)x.SoTienApDung, ct) ?? 0;
            var refunded = await dbContext.Set<PhieuHoanTien>().Where(x => x.PhieuCocId == deposit.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
            var take = Math.Min(remaining, Math.Max(0, deposit.SoTien - used - refunded));
            if (take > 0) { dbContext.ApDungCocHoaDons.Add(new ApDungCocHoaDon { HoaDonPhongId = invoice.Id, PhieuCocId = deposit.Id, SoTienApDung = take }); remaining -= take; }
        }
        await dbContext.SaveChangesAsync(ct); return invoice;
    }
    private static InvoiceResponse ToInvoiceResponse(HoaDonPhong invoice, long bookingId) => new(invoice.Id, $"invoices/{invoice.Id}", bookingId, invoice.TienPhong, invoice.TienPhuThu, invoice.TienBoiThuong, invoice.TienThue, invoice.TongTien, 0, 0, invoice.TongTien, invoice.TrangThai);

    public async Task<PricePreviewResponse> PreviewPriceAsync(long roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
    {
        ValidateDates(checkIn, checkOut);
        var room = await dbContext.Phongs.AsNoTracking().Include(x => x.LoaiPhong)
            .SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Room rooms/{roomId} was not found.");
        var prices = await CalculateNightsAsync(room, checkIn, checkOut, cancellationToken);
        return new PricePreviewResponse(roomId, checkIn, checkOut, prices, prices.Sum(x => x.FinalPrice));
    }
    public async Task<BookingResponse> CreateBookingAsync(long accountId, CreateBookingRequest request, CancellationToken cancellationToken)
        => await CreateBookingCoreAsync(accountId, null, "WEB", request, cancellationToken);

    public async Task<BookingResponse> CreateCounterBookingAsync(long customerId, string channel, CreateBookingRequest request, CancellationToken ct)
    {
        Input.OneOf(channel, "QUAY", "DIEN_THOAI");
        await actor.EmployeeIdAsync(ct);
        return await CreateBookingCoreAsync(actor.AccountId, customerId, channel, request, ct);
    }

    private async Task<BookingResponse> CreateBookingCoreAsync(long accountId, long? customerId, string channel, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var checkIn = HotelTime.StayDate(HotelTime.ToUtc(request.CheckIn));
        var checkOut = HotelTime.StayDate(HotelTime.ToUtc(request.CheckOut));
        ValidateDates(checkIn, checkOut);
        if (HotelTime.ToUtc(request.CheckOut) <= DateTime.UtcNow) throw new InvalidArgumentException("The stay has already ended.");
        Input.Optional(request.SpecialRequest, 1000, "special_request");
        if (request.RoomIds is null || request.RoomIds.Count == 0 || request.RoomIds.Distinct().Count() != request.RoomIds.Count)
            throw new InvalidArgumentException("room_ids must contain at least one unique room.");
        if (request.Adults < 1 || request.Children < 0)
            throw new InvalidArgumentException("adults must be at least 1 and children cannot be negative.");

        var customer = await dbContext.KhachHangs.SingleOrDefaultAsync(x => customerId.HasValue ? x.Id == customerId : x.TaiKhoanId == accountId, cancellationToken)
            ?? throw new PermissionDeniedException("The authenticated account is not linked to a customer profile.");
        var rooms = await dbContext.Phongs.Include(x => x.LoaiPhong)
            .Where(x => request.RoomIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (rooms.Count != request.RoomIds.Count)
            throw new ResourceNotFoundException("One or more selected rooms were not found.");
        if (rooms.Any(x => x.TrangThai is "BAO_TRI" or "NGUNG_SU_DUNG" || x.LoaiPhong.TrangThai != "HOAT_DONG"))
            throw new AlreadyExistsException("One or more selected rooms are not available.");
        if (rooms.Sum(x => x.LoaiPhong.SoNguoiLonToiDa) < request.Adults || rooms.Sum(x => x.LoaiPhong.SoTreEmToiDa) < request.Children)
            throw new InvalidArgumentException("Selected rooms cannot accommodate all guests.");

        var now = DateTime.UtcNow;
        var holdExpiresAt = now.Add(HoldDuration);
        await using var transaction = await BookingTransaction.BeginAsync(dbContext, cancellationToken);
        try
        {
            dbContext.KhoaLichPhongs.RemoveRange(await dbContext.KhoaLichPhongs
                .Where(x => x.TrangThai == "HOLD" && x.HetHanGiuCho <= now).ToListAsync(cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);

            var booking = new DatPhong
            {
                MaDatPhong = $"DP{Guid.NewGuid():N}",
                KhachHangId = customer.Id,
                TaiKhoanTaoId = accountId,
                NgayDat = now,
                DuKienNhan = HotelTime.ToUtc(request.CheckIn),
                DuKienTra = HotelTime.ToUtc(request.CheckOut),
                SoNguoiLon = request.Adults,
                SoTreEm = request.Children,
                KenhDat = channel,
                TrangThai = "HOLD",
                YeuCauDacBiet = request.SpecialRequest,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.DatPhongs.Add(booking);
            booking.ChinhSachCocId = await dbContext.Set<ChinhSachCoc>().Where(x => x.TrangThai == "HOAT_DONG" && (x.TuNgay == null || x.TuNgay <= checkIn) && (x.DenNgay == null || x.DenNgay >= checkIn)).OrderByDescending(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
            booking.ChinhSachHuyId = await dbContext.Set<ChinhSachHuy>().Where(x => x.TrangThai == "HOAT_DONG").OrderByDescending(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var details = new List<(ChiTietDatPhong Detail, IReadOnlyList<NightPriceResponse> Prices)>();
            var adultsLeft = request.Adults; var childrenLeft = request.Children;
            foreach (var room in rooms)
            {
                var prices = await CalculateNightsAsync(room, checkIn, checkOut, cancellationToken);
                var detail = new ChiTietDatPhong
                {
                    DatPhongId = booking.Id,
                    PhongId = room.Id,
                    SoNguoiLon = Math.Min(adultsLeft, room.LoaiPhong.SoNguoiLonToiDa),
                    SoTreEm = Math.Min(childrenLeft, room.LoaiPhong.SoTreEmToiDa),
                    TienPhongDuKien = prices.Sum(x => x.FinalPrice),
                    TrangThai = "HOLD"
                };
                details.Add((detail, prices));
                adultsLeft -= detail.SoNguoiLon; childrenLeft -= detail.SoTreEm;
                dbContext.ChiTietDatPhongs.Add(detail);
            }
            booking.TongTienPhongGoc = details.Sum(x => x.Prices.Sum(p => p.BasePrice));
            booking.TongTienDuKien = details.Sum(x => x.Prices.Sum(p => p.FinalPrice));
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var (detail, prices) in details)
            {
                dbContext.GiaDemDatPhongs.AddRange(prices.Select(price => new GiaDemDatPhong
                {
                    ChiTietDatPhongId = detail.Id,
                    NgayLuuTru = price.StayDate,
                    GiaCoBan = price.BasePrice,
                    PhuThuPhong = price.RoomSurcharge,
                    ChinhSachGiaId = price.PricingPolicyId,
                    TienDieuChinh = price.Adjustment,
                    GiaSauDieuChinh = price.FinalPrice
                }));
                dbContext.KhoaLichPhongs.AddRange(prices.Select(price => new KhoaLichPhong
                {
                    ChiTietDatPhongId = detail.Id,
                    PhongId = detail.PhongId,
                    NgayLuuTru = price.StayDate,
                    TrangThai = "HOLD",
                    HetHanGiuCho = holdExpiresAt,
                    CreatedAt = now
                }));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await notifications.BookingAsync(booking, accountId, "", "Đặt phòng mới đang chờ cọc.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(booking, details.SelectMany(x => x.Prices).ToArray(), holdExpiresAt);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new AlreadyExistsException("One or more selected rooms are already held or booked for the requested nights.");
        }
        catch (MySqlException exception) when (exception.Number is 1213 or 1205)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new AlreadyExistsException("The room inventory is busy. Please retry the booking.");
        }
    }
    public async Task<BookingResponse> ConfirmDepositAsync(long accountId, long bookingId, ConfirmDepositRequest request, CancellationToken cancellationToken)
    {
        Input.Money(request.Amount, "amount"); Input.Text(request.TransactionId, 150, "transaction_id");
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.PaymentGateway) || string.IsNullOrWhiteSpace(request.TransactionId))
            throw new InvalidArgumentException("A positive amount, payment_gateway and transaction_id are required.");
        if (string.Equals(request.PaymentGateway.Trim(), "VietQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR deposits can only be confirmed after a verified inquiry.");

        await using var transaction = await BookingTransaction.BeginAsync(dbContext, cancellationToken);
        await LockBookingAsync(bookingId, cancellationToken);
        var booking = await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs)
            .SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Booking bookings/{bookingId} was not found.");

        var existingPayment = await dbContext.PhieuCocs.SingleOrDefaultAsync(x =>
            x.CongThanhToan == request.PaymentGateway && x.MaGiaoDich == request.TransactionId, cancellationToken);
        if (existingPayment is not null)
        {
            if (existingPayment.DatPhongId != bookingId || existingPayment.SoTien != request.Amount || existingPayment.PhuongThucId != request.PaymentMethodId)
                throw new AlreadyExistsException("The payment transaction was already used with different data.");
            return await LoadBookingResponseAsync(bookingId, cancellationToken);
        }

        if (booking.TrangThai is not ("HOLD" or "BOOKED"))
            throw new InvalidArgumentException($"Booking is in state {booking.TrangThai} and cannot be confirmed.");
        var previousStatus = booking.TrangThai;
        if (await dbContext.Set<DepositPaymentOrder>().AnyAsync(x => x.BookingId == bookingId && x.Status == "PENDING", cancellationToken)) throw new InvalidArgumentException("Reconcile the existing deposit QR before recording a manual deposit.");
        var now = DateTime.UtcNow;
        var locks = await dbContext.KhoaLichPhongs.Where(x => x.ChiTietDatPhongId != 0 && booking.ChiTietDatPhongs.Select(d => d.Id).Contains(x.ChiTietDatPhongId)).ToListAsync(cancellationToken);
        if (previousStatus == "HOLD" && (locks.Count == 0 || locks.Any(x => x.TrangThai != "HOLD" || x.HetHanGiuCho <= now)))
            throw new AlreadyExistsException("The booking hold has expired or is no longer available.");
        var received = await dbContext.PhieuCocs.Where(x => x.DatPhongId == bookingId && x.TrangThai == "DA_THANH_TOAN").SumAsync(x => (decimal?)x.SoTien, cancellationToken) ?? 0;
        if (request.Amount + received > booking.TongTienDuKien)
            throw new InvalidArgumentException("Deposit amount cannot exceed the booking total.");
        if (request.Amount + received < await RequiredDepositAsync(booking, cancellationToken)) throw new InvalidArgumentException("Deposit is below the configured minimum.");
        var method = await dbContext.PhuongThucThanhToans.SingleOrDefaultAsync(x => x.Id == request.PaymentMethodId, cancellationToken) ?? throw new ResourceNotFoundException("Payment method was not found.");
        Input.OneOf(method.MaPhuongThuc, "TIEN_MAT", "CHUYEN_KHOAN", "THE", "CASH", "BANK_TRANSFER", "CARD");
        Input.OneOf(request.PaymentGateway, "MANUAL", "CASH", "BANK_TRANSFER", "CARD");
        if (request.ShiftId.HasValue && !await dbContext.CaLamViecs.AnyAsync(x => x.Id == request.ShiftId && x.NhanVienId == (dbContext.NhanViens.Where(n => n.TaiKhoanId == accountId).Select(n => n.Id).First()) && x.TrangThai == "DANG_MO", cancellationToken)) throw new InvalidArgumentException("Shift must belong to the cashier and be open.");
        if (!await dbContext.PhuongThucThanhToans.AnyAsync(x => x.Id == request.PaymentMethodId && x.TrangThai == "HOAT_DONG", cancellationToken))
            throw new ResourceNotFoundException($"Payment method {request.PaymentMethodId} was not found.");

        dbContext.PhieuCocs.Add(new PhieuCoc
        {
            MaPhieuCoc = $"PC{Guid.NewGuid():N}",
            DatPhongId = bookingId,
            TaiKhoanThanhToanId = accountId,
            NhanVienXacNhanId = await actor.EmployeeIdAsync(cancellationToken),
            CaLamViecId = request.ShiftId,
            PhuongThucId = request.PaymentMethodId,
            SoTien = request.Amount,
            CongThanhToan = request.PaymentGateway,
            MaGiaoDich = request.TransactionId,
            ThoiGianCoc = now,
            TrangThai = "DA_THANH_TOAN"
        });
        booking.TrangThai = "BOOKED";
        booking.UpdatedAt = now;
        foreach (var detail in booking.ChiTietDatPhongs) detail.TrangThai = "BOOKED";
        foreach (var hold in locks) { hold.TrangThai = "BOOKED"; hold.HetHanGiuCho = null; }
        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.BookingAsync(booking, accountId, previousStatus, "Đã xác nhận cọc tại quầy.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadBookingResponseAsync(bookingId, cancellationToken);
    }

    private async Task<IReadOnlyList<NightPriceResponse>> CalculateNightsAsync(Phong room, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
    {
        var policies = await dbContext.ChinhSachGias.AsNoTracking().Include(x => x.CacThuApDung).Include(x => x.ChiTiet)
            .Where(x => x.TrangThai == "HOAT_DONG" && x.TuNgay <= checkOut && x.DenNgay >= checkIn)
            .ToListAsync(cancellationToken);
        var result = new List<NightPriceResponse>();
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            var weekday = (byte)(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);
            var policy = policies.Where(x => x.TuNgay <= date && x.DenNgay >= date && x.CacThuApDung.Any(d => d.Thu == weekday) && x.ChiTiet.Any(d => d.LoaiPhongId == room.LoaiPhongId))
                .OrderByDescending(x => x.MucUuTien).ThenByDescending(x => x.Id)
                .FirstOrDefault();
            var detail = policy?.ChiTiet.SingleOrDefault(x => x.LoaiPhongId == room.LoaiPhongId);
            var basePrice = room.LoaiPhong.GiaCoBanDem;
            var adjusted = basePrice;
            if (detail is not null)
            {
                adjusted = detail.KieuDieuChinh switch
                {
                    "GIA_CO_DINH" => detail.GiaTri,
                    "TANG_%" => basePrice * (1 + detail.GiaTri / 100m),
                    "GIAM_%" => basePrice * (1 - detail.GiaTri / 100m),
                    "TANG_TIEN" => basePrice + detail.GiaTri,
                    "GIAM_TIEN" => basePrice - detail.GiaTri,
                    _ => throw new InvalidArgumentException($"Unsupported pricing adjustment type {detail.KieuDieuChinh}.")
                };
            }
            adjusted = Math.Max(0, adjusted);
            adjusted = Input.Round(adjusted);
            result.Add(new NightPriceResponse(date, basePrice, room.PhuThuDemMacDinh, policy?.Id, adjusted - basePrice, adjusted + room.PhuThuDemMacDinh));
        }
        return result;
    }

    private async Task<BookingResponse> LoadBookingResponseAsync(long bookingId, CancellationToken cancellationToken)
    {
        var booking = await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleAsync(x => x.Id == bookingId, cancellationToken);
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var prices = await dbContext.GiaDemDatPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).OrderBy(x => x.NgayLuuTru).ToListAsync(cancellationToken);
        var responsePrices = prices.Select(x => new NightPriceResponse(x.NgayLuuTru, x.GiaCoBan, x.PhuThuPhong, x.ChinhSachGiaId, x.TienDieuChinh, x.GiaSauDieuChinh)).ToArray();
        var hold = await dbContext.KhoaLichPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).Select(x => x.HetHanGiuCho).MaxAsync(cancellationToken);
        return ToResponse(booking, responsePrices, hold);
    }

    private static BookingResponse ToResponse(DatPhong booking, IReadOnlyList<NightPriceResponse> prices, DateTime? hold) => new(
        $"bookings/{booking.Id}", booking.Id, booking.MaDatPhong, booking.TrangThai,
        DateTime.SpecifyKind(booking.DuKienNhan, DateTimeKind.Utc), DateTime.SpecifyKind(booking.DuKienTra, DateTimeKind.Utc),
        booking.TongTienDuKien, hold, prices);

    private static void ValidateDates(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn) throw new InvalidArgumentException("check_out must be after check_in.");
        if (checkIn < HotelTime.Today) throw new InvalidArgumentException("check_in cannot be in the past.");
        if (checkOut.DayNumber - checkIn.DayNumber > 365) throw new InvalidArgumentException("A booking cannot exceed 365 nights.");
    }

    private static bool IsDuplicateKey(DbUpdateException exception) => exception.InnerException is MySqlException { Number: 1062 };
}
