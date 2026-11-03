using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.FrontDesk;

public sealed partial class FrontDeskService
{
    // Tạo khách, giữ phòng và nhận cọc cùng một giao dịch; không sao chép nghiệp vụ booking.
    public async Task<FrontDeskBookingDetail> CreateAsync(long accountId, CreateDeskBookingRequest request, CancellationToken ct)
    {
        if (request.CustomerId.HasValue == (request.NewCustomer is not null))
            throw new InvalidArgumentException("Chọn một khách có sẵn hoặc nhập một khách mới.");
        if (request.Deposit is not null && !await actor.HasPermissionAsync("BILLING_PAYMENT_WRITE", ct))
            throw new PermissionDeniedException("Cần quyền thu tiền để tạo booking kèm cọc.");
        if (request.NewCustomer is not null && !await actor.HasPermissionAsync("CUSTOMER_WRITE", ct))
            throw new PermissionDeniedException("Cần quyền tạo khách hàng mới.");
        await using var tx = await BookingTransaction.BeginAsync(db, ct);
        var customerId = await ResolveCustomerAsync(request, DateTime.UtcNow, ct);
        var booking = await bookings.CreateCounterBookingAsync(customerId, request.Channel,
            new CreateBookingRequest(request.RoomIds, HotelTime.ToUtc(request.CheckIn), HotelTime.ToUtc(request.CheckOut),
                request.Adults, request.Children, request.SpecialRequest), ct);
        if (request.Deposit is not null)
            await ConfirmDepositAsync(accountId, booking.Id, request.Deposit, ct);
        await tx.CommitAsync(ct);
        return await GetDetailAsync(booking.Id, ct);
    }

    public async Task<FrontDeskBookingDetail> ConfirmDepositAsync(long accountId, long bookingId, DeskDepositRequest request, CancellationToken ct)
    {
        // Client cũ thiếu reference chỉ được dùng khóa cố định cho lần cọc đầu.
        var reference = request.Reference ?? $"DESK-FIRST-{bookingId}";
        await bookings.ConfirmDepositAsync(accountId, bookingId,
            new ConfirmDepositRequest(request.PaymentMethodId, request.Amount, "MANUAL", reference), ct);
        return await GetDetailAsync(bookingId, ct);
    }

    public async Task<ChangeResult> ChangeDatesAsync(long accountId, long bookingId, ChangeDatesRequest request, CancellationToken ct)
    {
        var before = await GetDetailAsync(bookingId, ct);
        await bookings.RescheduleAsync(bookingId, HotelTime.ToUtc(request.CheckIn), HotelTime.ToUtc(request.CheckOut),
            request.Reason ?? "Đổi ngày tại quầy", ct);
        return await ChangedAsync(bookingId, before.Summary.Total, "Đã cập nhật ngày lưu trú.", ct);
    }

    public async Task<ChangeResult> ChangeRoomAsync(long accountId, long bookingId, ChangeRoomRequest request, CancellationToken ct)
    {
        var before = await GetDetailAsync(bookingId, ct);
        await bookings.MoveRoomAsync(bookingId, request.DetailId, request.NewRoomId, request.Reason ?? "Đổi phòng tại quầy", ct);
        return await ChangedAsync(bookingId, before.Summary.Total, "Đã đổi phòng.", ct);
    }

    public async Task<ChangeResult> CancelAsync(long accountId, long bookingId, DeskCancelRequest request, CancellationToken ct)
    {
        var before = await GetDetailAsync(bookingId, ct);
        await bookings.CancelAsync(accountId, bookingId, new CancelBookingRequest(request.Reason), ct);
        return await ChangedAsync(bookingId, before.Summary.Total, "Yêu cầu hủy đang chờ duyệt.", ct);
    }

    private async Task<ChangeResult> ChangedAsync(long id, decimal oldTotal, string message, CancellationToken ct)
    {
        var detail = await GetDetailAsync(id, ct);
        return new(detail, oldTotal, detail.Summary.Total, message);
    }

    public async Task<FrontDeskBookingDetail> MarkNoShowAsync(long accountId, long bookingId, DeskNoteRequest request, CancellationToken ct)
    {
        await bookings.MarkNoShowAsync(bookingId, new MarkNoShowRequest(await actor.EmployeeIdAsync(ct), request.Note), ct);
        return await GetDetailAsync(bookingId, ct);
    }

    public async Task<CancellationRequestItem> ApproveCancellationAsync(long accountId, long requestId, DecideCancellationRequest request, CancellationToken ct)
    {
        await bookings.ApproveCancellationAsync(requestId,
            new ApproveCancellationRequest(await actor.EmployeeIdAsync(ct), request.ApprovedRefund, request.Note), ct);
        return await CancellationAsync(requestId, ct);
    }

    public async Task<CancellationRequestItem> RejectCancellationAsync(long accountId, long requestId, DeskNoteRequest request, CancellationToken ct)
    {
        await bookings.RejectCancellationAsync(requestId, request.Note ?? "Từ chối hủy tại quầy", ct);
        return await CancellationAsync(requestId, ct);
    }

    private async Task<CancellationRequestItem> CancellationAsync(long id, CancellationToken ct)
    {
        var request = await db.YeuCauHuyDatPhongs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var booking = await db.DatPhongs.AsNoTracking().Include(x => x.KhachHang).SingleAsync(x => x.Id == request.DatPhongId, ct);
        return ToItem(request, booking.MaDatPhong, booking.KhachHang.HoTen);
    }

    // Lưu khai báo trước nhận phòng; giao dịch ngoài rollback cả hai nếu nhận phòng thất bại.
    public async Task<FrontDeskBookingDetail> SaveGuestsAsync(long bookingId, IReadOnlyList<StayGuestDto> guests, CancellationToken ct)
    {
        await using var tx = await BookingTransaction.BeginAsync(db, ct);
        await bookings.ReplaceGuestsAsync(bookingId, guests.Select(g => new BookingGuest(g.DetailId, g.FullName,
            g.BirthDate, g.Gender, g.DocumentType, g.DocumentNumber, g.Nationality, g.IsRepresentative)).ToList(), ct);
        await tx.CommitAsync(ct);
        return await GetDetailAsync(bookingId, ct);
    }

    public async Task<FrontDeskBookingDetail> CheckInAsync(long accountId, long bookingId, DeskCheckInRequest request, CancellationToken ct)
    {
        if (request.AllowNotReadyRooms)
            throw new InvalidArgumentException("Cần hoàn tất dọn phòng hoặc đổi sang phòng sẵn sàng trước khi nhận phòng.");
        await using var tx = await BookingTransaction.BeginAsync(db, ct);
        if (request.Guests is { Count: > 0 }) await SaveGuestsAsync(bookingId, request.Guests, ct);
        await bookings.CheckInAsync(bookingId, new CheckInRequest(await actor.EmployeeIdAsync(ct)), ct);
        await tx.CommitAsync(ct);
        return await GetDetailAsync(bookingId, ct);
    }

    public async Task<CheckOutPreview> PreviewCheckOutAsync(long bookingId, CancellationToken ct)
    {
        var quote = await bookings.PreviewCheckoutAsync(bookingId, ct);
        return new(bookingId, quote.Code, HotelTime.Local(quote.CheckIn), HotelTime.Local(quote.CheckOut),
            HotelTime.Local(quote.ActualCheckIn), HotelTime.Local(DateTime.UtcNow), quote.RoomAmount,
            quote.EarlySurcharge, quote.LateSurcharge, quote.Compensation, quote.VatRate, quote.Deposit,
            quote.Total, quote.Due, ["Chốt hóa đơn và thu đủ tiền trước khi trả phòng. Phụ thu nhập thêm không bao gồm phụ thu tự động."]);
    }

    public async Task<DeskCheckOutResult> CheckOutAsync(long accountId, long bookingId, DeskCheckOutRequest request, CancellationToken ct)
    {
        // Không tự xác nhận đã thu tiền và không tạo thêm hóa đơn khi trả phòng.
        var invoice = await bookings.CheckOutAsync(bookingId,
            new CheckOutRequest(await actor.EmployeeIdAsync(ct), request.Surcharge, request.VatRate), ct);
        var line = (await BuildInvoiceLinesAsync(bookingId, ct)).Single(x => x.Id == invoice.Id);
        var rooms = await db.ChiTietDatPhongs.Where(x => x.DatPhongId == bookingId).Select(x => x.Phong.SoPhong).ToListAsync(ct);
        return new(line, rooms, "Đã trả phòng, hóa đơn đã thanh toán đủ và phòng chuyển sang Cần dọn.");
    }
}
