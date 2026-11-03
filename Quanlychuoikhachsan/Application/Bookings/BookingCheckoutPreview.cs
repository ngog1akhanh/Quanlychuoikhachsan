using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed record CheckoutQuote(string Code, DateTime CheckIn, DateTime CheckOut, DateTime? ActualCheckIn,
    decimal RoomAmount, decimal EarlySurcharge, decimal LateSurcharge, decimal Compensation, decimal VatRate,
    decimal Deposit, decimal Total, decimal Due);

public sealed partial class BookingService
{
    // Dự toán không ghi dữ liệu; hóa đơn đã phát hành luôn là nguồn số tiền chính thức.
    public async Task<CheckoutQuote> PreviewCheckoutAsync(long bookingId, CancellationToken ct)
    {
        var booking = await LoadBookingForBillingAsync(bookingId, ct);
        if (booking.TrangThai != "DANG_O") throw new InvalidArgumentException("Only an active stay can check out.");
        var issued = await dbContext.HoaDonPhongs.SingleOrDefaultAsync(x => x.DatPhongId == bookingId && x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY", ct);
        if (issued is not null)
        {
            var invoice = await InvoiceResponseAsync(issued, ct);
            var taxable = invoice.RoomAmount + invoice.Surcharge + invoice.Compensation;
            return new(booking.MaDatPhong, booking.DuKienNhan, booking.DuKienTra, booking.ChiTietDatPhongs.Min(x => x.NhanPhongLuc),
                invoice.RoomAmount, 0, invoice.Surcharge, invoice.Compensation, taxable == 0 ? 0 : invoice.Vat / taxable,
                invoice.DepositApplied, invoice.Total, invoice.Due);
        }

        var ids = booking.ChiTietDatPhongs.Select(x => x.Id).ToArray();
        var roomAmount = await dbContext.GiaDemDatPhongs.Where(x => ids.Contains(x.ChiTietDatPhongId)).SumAsync(x => x.GiaSauDieuChinh, ct);
        var compensation = await dbContext.TinhTrangPhongs.Where(x => x.DatPhongId == bookingId && x.DoKhachGayRa).SumAsync(x => (decimal?)x.ChiPhiThucTe, ct) ?? 0;
        var parts = await SurchargePartsAsync(booking, ct);
        var taxableAmount = roomAmount + compensation + parts.Early + parts.Late;
        var total = taxableAmount + Input.Round(taxableAmount * 0.1m);
        var deposit = 0m;
        foreach (var item in await dbContext.PhieuCocs.Where(x => x.DatPhongId == bookingId && x.TrangThai == "DA_THANH_TOAN").ToListAsync(ct))
        {
            var reserved = await dbContext.Set<PhieuHoanTien>().Where(x => x.PhieuCocId == item.Id && x.TrangThai != "DA_HUY").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
            deposit += Math.Max(0, item.SoTien - reserved);
        }
        deposit = Math.Min(total, deposit);
        return new(booking.MaDatPhong, booking.DuKienNhan, booking.DuKienTra, booking.ChiTietDatPhongs.Min(x => x.NhanPhongLuc),
            roomAmount, parts.Early, parts.Late, compensation, 0.1m, deposit, total, Math.Max(0, total - deposit));
    }
}
