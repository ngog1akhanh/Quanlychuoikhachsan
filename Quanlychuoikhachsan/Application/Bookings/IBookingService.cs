using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Bookings;

public interface IBookingService
{
    Task<IReadOnlyList<RoomSummaryResponse>> FindAvailableRoomsAsync(RoomAvailabilityQuery query, CancellationToken cancellationToken);
    Task<PricePreviewResponse> PreviewPriceAsync(long roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken);
    Task<BookingResponse> CreateBookingAsync(long accountId, CreateBookingRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> UpdateBookingAsync(long accountId, long bookingId, PatchBookingRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> ConfirmDepositAsync(long accountId, long bookingId, ConfirmDepositRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> CheckInAsync(long bookingId, CheckInRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> CheckOutAsync(long bookingId, CheckOutRequest request, CancellationToken cancellationToken);
    Task<InvoiceResponse> AddInvoicePaymentAsync(long invoiceId, AddInvoicePaymentRequest request, CancellationToken cancellationToken);
    Task<CancellationRequestResponse> CancelAsync(long accountId, long bookingId, CancelBookingRequest request, CancellationToken cancellationToken);
    Task<CancellationRequestResponse> ApproveCancellationAsync(long requestId, ApproveCancellationRequest request, CancellationToken cancellationToken);
    Task<BookingResponse> MarkNoShowAsync(long bookingId, MarkNoShowRequest request, CancellationToken cancellationToken);
}

/// <summary>Th?c hi?n vòng ??i ??t phòng, ??nh giá, nh?n/tr? phòng và thu ti?n hóa ??n.</summary>
public sealed class BookingService(HotelDbContext dbContext) : IBookingService
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(15);

    // Tìm phòng không b? ??t ho?c gi? ch? trong kho?ng l?u trú.
    public async Task<IReadOnlyList<RoomSummaryResponse>> FindAvailableRoomsAsync(RoomAvailabilityQuery query, CancellationToken cancellationToken)
    {
        ValidateDates(query.CheckInDate, query.CheckOutDate);
        var occupiedRoomIds = dbContext.KhoaLichPhongs
            .Where(x => x.NgayLuuTru >= query.CheckInDate && x.NgayLuuTru < query.CheckOutDate &&
                (x.TrangThai == "BOOKED" || (x.TrangThai == "HOLD" && x.HetHanGiuCho > DateTime.UtcNow)))
            .Select(x => x.PhongId);

        return await dbContext.Phongs.AsNoTracking()
            .Where(x => x.TrangThai == "SAN_SANG" && !occupiedRoomIds.Contains(x.Id))
            .Where(x => x.LoaiPhong.SoNguoiLonToiDa >= query.Adults && x.LoaiPhong.SoTreEmToiDa >= query.Children)
            .Select(x => new RoomSummaryResponse(x.Id, x.MaPhong, x.SoPhong, x.LoaiPhong.MaLoaiPhong, x.LoaiPhong.TenLoaiPhong, x.LoaiPhong.GiaCoBanDem))
            .ToListAsync(cancellationToken);
    }

    public async Task<BookingResponse> UpdateBookingAsync(long accountId, long bookingId, PatchBookingRequest request, CancellationToken ct)
    {
        if (request.SpecialRequest?.Length > 1000)
            throw new InvalidArgumentException("special_request cannot exceed 1000 characters.");

        var booking = await dbContext.DatPhongs.SingleOrDefaultAsync(
            x => x.Id == bookingId && x.TaiKhoanTaoId == accountId, ct)
            ?? throw new ResourceNotFoundException($"Booking bookings/{bookingId} was not found.");

        if (booking.TrangThai != "HOLD")
            throw new InvalidArgumentException("Only a booking on hold can be updated.");

        booking.YeuCauDacBiet = request.SpecialRequest;
        booking.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return await LoadBookingResponseAsync(bookingId, ct);
    }

    // Chuy?n ??t phòng sang ?ang ? và t?o snapshot hóa ??n ban ??u.
    public async Task<InvoiceResponse> CheckInAsync(long bookingId, CheckInRequest request, CancellationToken ct)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "BOOKED") throw new InvalidArgumentException("Only a booked reservation can be checked in.");
        foreach (var detail in booking.ChiTietDatPhongs) { detail.NhanPhongLuc = DateTime.UtcNow; detail.TrangThai = "DANG_O"; }
        booking.TrangThai = "DANG_O"; booking.UpdatedAt = DateTime.UtcNow;
        var invoice = await CreateInvoiceAsync(booking, request.EmployeeId, 0, 0, ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ToInvoiceResponse(invoice, bookingId);
    }

    // Ch?t th?i ?i?m tr? phòng và t?o hóa ??n cu?i cùng trong transaction.
    public async Task<InvoiceResponse> CheckOutAsync(long bookingId, CheckOutRequest request, CancellationToken ct)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai is not ("DANG_O" or "BOOKED")) throw new InvalidArgumentException("The reservation is not active.");
        foreach (var detail in booking.ChiTietDatPhongs) { detail.TraPhongLuc = DateTime.UtcNow; detail.TrangThai = "DA_TRA"; }
        booking.TrangThai = "DA_TRA"; booking.UpdatedAt = DateTime.UtcNow;
        var invoice = await CreateInvoiceAsync(booking, request.EmployeeId, request.EarlyOrLateSurcharge, request.VatRate, ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ToInvoiceResponse(invoice, bookingId);
    }

    // Ch?n thanh toán th? công gi? danh VietQR và tránh ghi nh?n khi QR c? còn có th? nh?n ti?n.
    public async Task<InvoiceResponse> AddInvoicePaymentAsync(long invoiceId, AddInvoicePaymentRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new InvalidArgumentException("Payment amount must be positive.");
        if (string.Equals(request.PaymentGateway?.Trim(), "VietQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR payments can only be recorded after a verified inquiry.");
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoice = await dbContext.HoaDonPhongs.Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw new ResourceNotFoundException("Room invoice was not found.");
        // Local CANCELLED/FAILED không h?y QR bên provider nên m?i ??n ch?a ??i soát v?n ph?i ch?n.
        if (await dbContext.YeuCauThanhToanVietQrs.AnyAsync(
            x => x.HoaDonPhongId == invoiceId && x.TrangThai != "PAID" && x.TrangThai != "REFUNDED", ct))
            throw new InvalidArgumentException("Resolve the existing VietQR order before recording a manual invoice payment; changing its local status does not invalidate its QR code.");
        var paymentMethod = await dbContext.PhuongThucThanhToans.SingleOrDefaultAsync(
            x => x.Id == request.PaymentMethodId && x.TrangThai == "HOAT_DONG", ct)
            ?? throw new ResourceNotFoundException("Payment method was not found.");
        if (string.Equals(paymentMethod.MaPhuongThuc, "VIETQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR payments can only be recorded after a verified inquiry.");
        var paid = await dbContext.ThanhToanHoaDonPhongs.Where(x => x.HoaDonPhongId == invoiceId && x.TrangThai == "THANH_CONG").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
        var deposit = await dbContext.ApDungCocHoaDons.Where(x => x.HoaDonPhongId == invoiceId).SumAsync(x => (decimal?)x.SoTienApDung, ct) ?? 0;
        if (paid + deposit + request.Amount > invoice.TongTien) throw new InvalidArgumentException("Payment exceeds the remaining invoice balance.");
        dbContext.ThanhToanHoaDonPhongs.Add(new ThanhToanHoaDonPhong { MaThanhToan = $"TT{Guid.NewGuid():N}"[..40], HoaDonPhongId = invoiceId, NhanVienThuId = request.EmployeeId, PhuongThucId = request.PaymentMethodId, SoTien = request.Amount, ThoiGianThanhToan = DateTime.UtcNow, CongThanhToan = request.PaymentGateway, MaGiaoDich = request.TransactionId });
        invoice.TrangThai = paid + deposit + request.Amount >= invoice.TongTien ? "DA_THANH_TOAN" : "TT_MOT_PHAN";
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ToInvoiceResponse(invoice, invoice.DatPhongId);
    }

    public async Task<CancellationRequestResponse> CancelAsync(long accountId, long bookingId, CancelBookingRequest request, CancellationToken ct)
    {
        var booking = await dbContext.DatPhongs.Include(x => x.KhachHang).SingleOrDefaultAsync(x => x.Id == bookingId && x.TaiKhoanTaoId == accountId, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
        if (booking.TrangThai is "DA_HUY" or "DA_TRA" or "NO_SHOW") throw new InvalidArgumentException("The booking cannot be cancelled in its current state.");
        var item = new YeuCauHuyDatPhong { MaYeuCau = $"YC{Guid.NewGuid():N}"[..40], DatPhongId = bookingId, KhachHangId = booking.KhachHangId, LyDo = request.Reason, ThoiGianYeuCau = DateTime.UtcNow, SoTienHoanDuKien = await dbContext.PhieuCocs.Where(x => x.DatPhongId == bookingId && x.TrangThai == "DA_THANH_TOAN").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0 };
        dbContext.YeuCauHuyDatPhongs.Add(item); await dbContext.SaveChangesAsync(ct); return new(item.Id, $"cancellation-requests/{item.Id}", bookingId, item.TrangThai, item.SoTienHoanDuKien, item.SoTienHoanDuyet);
    }

    public async Task<CancellationRequestResponse> ApproveCancellationAsync(long requestId, ApproveCancellationRequest request, CancellationToken ct)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var item = await dbContext.YeuCauHuyDatPhongs.SingleOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new ResourceNotFoundException("Cancellation request was not found.");
        if (item.TrangThai != "CHO_DUYET" || request.ApprovedRefund < 0 || request.ApprovedRefund > item.SoTienHoanDuKien) throw new InvalidArgumentException("Cancellation request or refund amount is invalid.");
        var booking = await dbContext.DatPhongs.SingleAsync(x => x.Id == item.DatPhongId, ct); booking.TrangThai = "DA_HUY"; booking.UpdatedAt = DateTime.UtcNow;
        item.TrangThai = "DA_DUYET"; item.SoTienHoanDuyet = request.ApprovedRefund; item.NhanVienXuLyId = request.EmployeeId; item.ThoiGianXuLy = DateTime.UtcNow; item.GhiChuXuLy = request.Note;
        await dbContext.KhoaLichPhongs.Where(x => x.ChiTietDatPhongId != 0 && dbContext.ChiTietDatPhongs.Where(d => d.DatPhongId == item.DatPhongId).Select(d => d.Id).Contains(x.ChiTietDatPhongId)).ExecuteDeleteAsync(ct);
        await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(item.Id, $"cancellation-requests/{item.Id}", item.DatPhongId, item.TrangThai, item.SoTienHoanDuKien, item.SoTienHoanDuyet);
    }

    public async Task<BookingResponse> MarkNoShowAsync(long bookingId, MarkNoShowRequest request, CancellationToken ct)
    {
        var booking = await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleOrDefaultAsync(x => x.Id == bookingId, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
        if (booking.TrangThai != "BOOKED") throw new InvalidArgumentException("Only booked reservations can be marked as no-show.");
        booking.TrangThai = "NO_SHOW"; booking.UpdatedAt = DateTime.UtcNow; await dbContext.SaveChangesAsync(ct); return await LoadBookingResponseAsync(bookingId, ct);
    }

    private async Task<DatPhong> LoadBookingForBillingAsync(long id, CancellationToken ct) => await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Booking was not found.");
    private async Task<HoaDonPhong> CreateInvoiceAsync(DatPhong booking, long employeeId, decimal surcharge, decimal vatRate, CancellationToken ct)
    {
        var detailIds = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var nights = await dbContext.GiaDemDatPhongs.Where(x => detailIds.Contains(x.ChiTietDatPhongId)).ToListAsync(ct);
        var roomAmount = nights.Sum(x => x.GiaSauDieuChinh);
        var compensation = await dbContext.TinhTrangPhongs.Where(x => x.DatPhongId == booking.Id && x.DoKhachGayRa).SumAsync(x => (decimal?)x.ChiPhiThucTe, ct) ?? 0;
        var taxable = roomAmount + surcharge + compensation;
        var invoice = new HoaDonPhong { MaHoaDon = $"HD{Guid.NewGuid():N}"[..40], DatPhongId = booking.Id, NhanVienLapId = employeeId, NgayLap = DateTime.UtcNow, TienPhong = roomAmount, TienPhuThu = surcharge, TienBoiThuong = compensation, TienThue = decimal.Round(taxable * vatRate, 2), TongTien = decimal.Round(taxable * (1 + vatRate), 2), TrangThai = "DA_PHAT_HANH" };
        foreach (var night in nights) invoice.ChiTiet.Add(new ChiTietHoaDonPhong { ChiTietDatPhongId = night.ChiTietDatPhongId, LoaiDong = "TIEN_PHONG", NoiDung = $"Room night {night.NgayLuuTru:yyyy-MM-dd}", NgayApDung = night.NgayLuuTru, SoLuong = 1, DonGia = night.GiaSauDieuChinh, ThanhTien = night.GiaSauDieuChinh });
        dbContext.HoaDonPhongs.Add(invoice); await dbContext.SaveChangesAsync(ct);
        var deposits = await dbContext.PhieuCocs.Where(x => x.DatPhongId == booking.Id && x.TrangThai == "DA_THANH_TOAN").ToListAsync(ct); foreach (var deposit in deposits) dbContext.ApDungCocHoaDons.Add(new ApDungCocHoaDon { HoaDonPhongId = invoice.Id, PhieuCocId = deposit.Id, SoTienApDung = deposit.SoTien });
        return invoice;
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

    // Gi? l?ch phòng có th?i h?n và t?o booking cùng giá t?ng ?êm ?ã ch?t.
    public async Task<BookingResponse> CreateBookingAsync(long accountId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var checkIn = DateOnly.FromDateTime(request.CheckIn.Date);
        var checkOut = DateOnly.FromDateTime(request.CheckOut.Date);
        ValidateDates(checkIn, checkOut);
        if (request.RoomIds is null || request.RoomIds.Count == 0 || request.RoomIds.Distinct().Count() != request.RoomIds.Count)
            throw new InvalidArgumentException("room_ids must contain at least one unique room.");
        if (request.Adults < 1 || request.Children < 0)
            throw new InvalidArgumentException("adults must be at least 1 and children cannot be negative.");

        var customer = await dbContext.KhachHangs.SingleOrDefaultAsync(x => x.TaiKhoanId == accountId, cancellationToken)
            ?? throw new PermissionDeniedException("The authenticated account is not linked to a customer profile.");
        var rooms = await dbContext.Phongs.Include(x => x.LoaiPhong)
            .Where(x => request.RoomIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (rooms.Count != request.RoomIds.Count)
            throw new ResourceNotFoundException("One or more selected rooms were not found.");
        if (rooms.Any(x => x.TrangThai != "SAN_SANG"))
            throw new AlreadyExistsException("One or more selected rooms are not available.");

        var now = DateTime.UtcNow;
        var holdExpiresAt = now.Add(HoldDuration);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await dbContext.KhoaLichPhongs
                .Where(x => x.TrangThai == "HOLD" && x.HetHanGiuCho <= now)
                .ExecuteDeleteAsync(cancellationToken);

            var booking = new DatPhong
            {
                MaDatPhong = $"DP{Guid.NewGuid():N}"[..40],
                KhachHangId = customer.Id,
                TaiKhoanTaoId = accountId,
                NgayDat = now,
                DuKienNhan = request.CheckIn.ToUniversalTime(),
                DuKienTra = request.CheckOut.ToUniversalTime(),
                SoNguoiLon = request.Adults,
                SoTreEm = request.Children,
                KenhDat = "WEB",
                TrangThai = "HOLD",
                YeuCauDacBiet = request.SpecialRequest,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.DatPhongs.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);

            var details = new List<(ChiTietDatPhong Detail, IReadOnlyList<NightPriceResponse> Prices)>();
            foreach (var room in rooms)
            {
                var prices = await CalculateNightsAsync(room, checkIn, checkOut, cancellationToken);
                var detail = new ChiTietDatPhong
                {
                    DatPhongId = booking.Id,
                    PhongId = room.Id,
                    SoNguoiLon = request.Adults,
                    SoTreEm = request.Children,
                    TienPhongDuKien = prices.Sum(x => x.FinalPrice),
                    TrangThai = "HOLD"
                };
                details.Add((detail, prices));
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

    // Xác nh?n ti?n c?c và chuy?n booking kh?i tr?ng thái gi? ch? khi ?? ?i?u ki?n.
    public async Task<BookingResponse> ConfirmDepositAsync(long accountId, long bookingId, ConfirmDepositRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.PaymentGateway) || string.IsNullOrWhiteSpace(request.TransactionId))
            throw new InvalidArgumentException("A positive amount, payment_gateway and transaction_id are required.");
        if (string.Equals(request.PaymentGateway.Trim(), "VietQR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidArgumentException("VietQR deposits can only be confirmed after a verified inquiry.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var booking = await dbContext.DatPhongs.Include(x => x.ChiTietDatPhongs)
            .SingleOrDefaultAsync(x => x.Id == bookingId && x.TaiKhoanTaoId == accountId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Booking bookings/{bookingId} was not found.");

        var existingPayment = await dbContext.PhieuCocs.SingleOrDefaultAsync(x =>
            x.CongThanhToan == request.PaymentGateway && x.MaGiaoDich == request.TransactionId, cancellationToken);
        if (existingPayment is not null)
        {
            if (existingPayment.DatPhongId != bookingId)
                throw new AlreadyExistsException("The payment transaction is already linked to another booking.");
            return await LoadBookingResponseAsync(bookingId, cancellationToken);
        }

        if (booking.TrangThai != "HOLD")
            throw new InvalidArgumentException($"Booking is in state {booking.TrangThai} and cannot be confirmed.");
        var now = DateTime.UtcNow;
        var locks = await dbContext.KhoaLichPhongs.Where(x => x.ChiTietDatPhongId != 0 && booking.ChiTietDatPhongs.Select(d => d.Id).Contains(x.ChiTietDatPhongId)).ToListAsync(cancellationToken);
        if (locks.Any(x => x.TrangThai != "HOLD" || x.HetHanGiuCho <= now))
            throw new AlreadyExistsException("The booking hold has expired or is no longer available.");
        if (request.Amount > booking.TongTienDuKien)
            throw new InvalidArgumentException("Deposit amount cannot exceed the booking total.");
        if (!await dbContext.PhuongThucThanhToans.AnyAsync(x => x.Id == request.PaymentMethodId && x.TrangThai == "HOAT_DONG", cancellationToken))
            throw new ResourceNotFoundException($"Payment method {request.PaymentMethodId} was not found.");

        dbContext.PhieuCocs.Add(new PhieuCoc
        {
            MaPhieuCoc = $"PC{Guid.NewGuid():N}"[..40],
            DatPhongId = bookingId,
            TaiKhoanThanhToanId = accountId,
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
            var policy = policies.Where(x => x.TuNgay <= date && x.DenNgay >= date && x.CacThuApDung.Any(d => d.Thu == weekday))
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
        if (checkIn < DateOnly.FromDateTime(DateTime.UtcNow.Date)) throw new InvalidArgumentException("check_in cannot be in the past.");
        if (checkOut.DayNumber - checkIn.DayNumber > 365) throw new InvalidArgumentException("A booking cannot exceed 365 nights.");
    }

    private static bool IsDuplicateKey(DbUpdateException exception) => exception.InnerException is MySqlException { Number: 1062 };
}
