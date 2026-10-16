using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Payments;

/// <summary>Tạo yêu cầu thanh toán hóa đơn và chỉ xác nhận PAID sau khi đối soát Inquiry VietQR.</summary>
public sealed class VietQrPaymentService(
    HotelDbContext dbContext,
    IVietQrGateway vietQrGateway,
    IOptions<VietQrSettings> settings,
    ILogger<VietQrPaymentService> logger) : IVietQrPaymentService
{
    private const string VietQrPaymentMethodCode = "VIETQR";

    // Tính số dư phía server, kiểm tra chủ hóa đơn và lưu QR VietQR vào đơn PENDING.
    public async Task<VietQrPaymentResponse> CreateInvoicePaymentAsync(
        long accountId,
        long invoiceId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var invoice = await dbContext.HoaDonPhongs.SingleOrDefaultAsync(
            item => item.Id == invoiceId, cancellationToken)
            ?? throw new ResourceNotFoundException("Room invoice was not found.");
        if (!await dbContext.DatPhongs.AnyAsync(
            booking => booking.Id == invoice.DatPhongId && booking.TaiKhoanTaoId == accountId,
            cancellationToken))
            throw new ResourceNotFoundException("Room invoice was not found.");
        if (invoice.TrangThai is "DA_HUY" or "DA_THANH_TOAN")
            throw new InvalidArgumentException("The invoice is not payable in its current state.");

        // Không tạo QR khác nếu QR cũ chưa được đối soát; trạng thái hủy cục bộ không hủy QR phía VietQR.
        var pendingOrder = await dbContext.YeuCauThanhToanVietQrs
            .Where(order => order.HoaDonPhongId == invoiceId &&
                order.TrangThai != "PAID" && order.TrangThai != "REFUNDED")
            .OrderByDescending(order => order.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (pendingOrder is not null)
        {
            if (pendingOrder.TrangThai != "PENDING")
                throw new InvalidArgumentException("An existing VietQR order must be reconciled through inquiry before creating another QR.");
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(pendingOrder);
        }

        var amount = await GetRemainingBalanceAsync(invoice, cancellationToken);
        if (amount <= 0)
            throw new InvalidArgumentException("The invoice has no remaining balance.");
        if (amount != decimal.Truncate(amount) || amount > long.MaxValue)
            throw new InvalidArgumentException("The remaining invoice balance must be a whole VND amount.");

        var orderId = Guid.NewGuid().ToString("N");
        var description = $"HD{invoiceId} {orderId}";
        var qr = await vietQrGateway.CreateCustomerQrAsync(
            new VietQrCreateCustomerQrRequest(
                decimal.ToInt64(amount),
                description,
                settings.Value.BankAccount,
                settings.Value.BankCode,
                settings.Value.UserBankName,
                "C",
                orderId,
                0),
            cancellationToken);

        var now = DateTime.UtcNow;
        var paymentOrder = new YeuCauThanhToanVietQr
        {
            OrderId = orderId,
            HoaDonPhongId = invoiceId,
            SoTien = amount,
            NoiDung = description,
            QrCode = qr.QrCode,
            QrDataUrl = qr.QrDataUrl,
            TrangThai = "PENDING",
            TaiKhoanTaoId = accountId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.YeuCauThanhToanVietQrs.Add(paymentOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(paymentOrder);
    }

    // Chỉ cho chủ đơn gọi Inquiry; đối chiếu order/amount/transaction trước khi ghi sổ một lần.
    public async Task<VietQrPaymentResponse> InquirePaymentAsync(
        long accountId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var paymentOrder = await dbContext.YeuCauThanhToanVietQrs.SingleOrDefaultAsync(
            order => order.OrderId == orderId && order.TaiKhoanTaoId == accountId,
            cancellationToken)
            ?? throw new ResourceNotFoundException("VietQR payment order was not found.");

        if (paymentOrder.TrangThai == "PAID")
            return ToResponse(paymentOrder);
        if (paymentOrder.TrangThai == "REFUNDED")
            throw new InvalidArgumentException("This order requires manual refund reconciliation.");

        var inquiry = await vietQrGateway.InquireAsync(paymentOrder.OrderId, cancellationToken);
        if (!string.Equals(inquiry.Code, "00", StringComparison.Ordinal) ||
            !string.Equals(inquiry.OrderId, paymentOrder.OrderId, StringComparison.Ordinal) ||
            inquiry.Amount != paymentOrder.SoTien)
        {
            logger.LogWarning("VietQR inquiry did not match local order {OrderId}.", paymentOrder.OrderId);
            throw new ServiceUnavailableException("VietQR inquiry did not match the stored payment order.");
        }

        if (!string.Equals(inquiry.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(inquiry.Status, "PAID", StringComparison.OrdinalIgnoreCase))
            return ToResponse(paymentOrder);
        if (string.IsNullOrWhiteSpace(inquiry.TransactionId))
            throw new ServiceUnavailableException("VietQR reported a successful payment without a transaction ID.");

        // Đọc lại trạng thái trong transaction để các lần Inquiry đồng thời không ghi nhận trùng.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        paymentOrder = await dbContext.YeuCauThanhToanVietQrs.SingleOrDefaultAsync(
            order => order.OrderId == orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("VietQR payment order was not found.");

        if (paymentOrder.TrangThai == "PAID")
        {
            if (!string.Equals(paymentOrder.MaGiaoDich, inquiry.TransactionId, StringComparison.Ordinal))
                throw new InvalidArgumentException("The paid order was reported with a different transaction ID.");
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(paymentOrder);
        }
        if (paymentOrder.TrangThai == "REFUNDED")
            throw new InvalidArgumentException("This order requires manual refund reconciliation.");
        if (await dbContext.ThanhToanHoaDonPhongs.AnyAsync(
            payment => payment.CongThanhToan == "VietQR" && payment.MaGiaoDich == inquiry.TransactionId,
            cancellationToken))
            throw new InvalidArgumentException("This VietQR transaction has already been recorded for another payment order.");

        var invoice = await dbContext.HoaDonPhongs.SingleOrDefaultAsync(
            item => item.Id == paymentOrder.HoaDonPhongId, cancellationToken)
            ?? throw new ResourceNotFoundException("Room invoice was not found.");
        if (invoice.TrangThai is "DA_HUY" or "DA_THANH_TOAN")
            throw new InvalidArgumentException("The invoice is no longer payable; reconcile the received VietQR payment manually.");

        var method = await dbContext.PhuongThucThanhToans.SingleOrDefaultAsync(
            item => item.MaPhuongThuc == VietQrPaymentMethodCode && item.TrangThai == "HOAT_DONG",
            cancellationToken)
            ?? throw new InvalidOperationException("The VIETQR payment method has not been configured in the database.");

        var paid = await dbContext.ThanhToanHoaDonPhongs
            .Where(item => item.HoaDonPhongId == invoice.Id && item.TrangThai == "THANH_CONG")
            .SumAsync(item => (decimal?)item.SoTien, cancellationToken) ?? 0;
        var deposits = await dbContext.ApDungCocHoaDons
            .Where(item => item.HoaDonPhongId == invoice.Id)
            .SumAsync(item => (decimal?)item.SoTienApDung, cancellationToken) ?? 0;
        if (paid + deposits + paymentOrder.SoTien > invoice.TongTien)
            throw new InvalidArgumentException("The verified payment exceeds the invoice's remaining balance; reconcile it manually.");

        var paidAt = DateTime.UtcNow;
        dbContext.ThanhToanHoaDonPhongs.Add(new ThanhToanHoaDonPhong
        {
            MaThanhToan = $"TT{Guid.NewGuid():N}"[..40],
            HoaDonPhongId = invoice.Id,
            PhuongThucId = method.Id,
            SoTien = paymentOrder.SoTien,
            ThoiGianThanhToan = ParseTransactionDate(inquiry.TransactionDateTime) ?? paidAt,
            CongThanhToan = "VietQR",
            MaGiaoDich = inquiry.TransactionId,
            TrangThai = "THANH_CONG"
        });

        paymentOrder.TrangThai = "PAID";
        paymentOrder.MaGiaoDich = inquiry.TransactionId;
        paymentOrder.TransactionDateTime = inquiry.TransactionDateTime;
        paymentOrder.PaidAt = paidAt;
        paymentOrder.UpdatedAt = paidAt;
        invoice.TrangThai = paid + deposits + paymentOrder.SoTien >= invoice.TongTien
            ? "DA_THANH_TOAN"
            : "TT_MOT_PHAN";

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(paymentOrder);
    }

    // Trả tối đa 100 đơn theo trạng thái cho người có quyền quản lý thanh toán.
    public async Task<IReadOnlyList<VietQrPaymentListResponse>> ListPaymentOrdersAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (normalizedStatus is not null && normalizedStatus is not ("PENDING" or "PAID" or "CANCELLED" or "FAILED" or "REFUNDED"))
            throw new InvalidArgumentException("status must be PENDING, PAID, CANCELLED, FAILED, or REFUNDED.");

        var query = dbContext.YeuCauThanhToanVietQrs.AsNoTracking();
        if (normalizedStatus is not null)
            query = query.Where(order => order.TrangThai == normalizedStatus);
        var orders = await query.OrderByDescending(order => order.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return orders.Select(ToListResponse).ToArray();
    }

    // Cho phép cập nhật ghi chú quản trị mà không tác động trạng thái thanh toán.
    public async Task<VietQrPaymentListResponse> UpdatePaymentNoteAsync(
        string orderId,
        string? note,
        CancellationToken cancellationToken)
    {
        if (note?.Length > 1000)
            throw new InvalidArgumentException("note cannot exceed 1000 characters.");
        var order = await dbContext.YeuCauThanhToanVietQrs.SingleOrDefaultAsync(
            item => item.OrderId == orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("VietQR payment order was not found.");
        order.GhiChu = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToListResponse(order);
    }

    // Chỉ cho phép đổi trạng thái cục bộ chưa thanh toán; không thể xác nhận PAID/REFUNDED thủ công.
    public async Task<VietQrPaymentListResponse> UpdatePaymentStatusAsync(
        string orderId,
        string status,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = status.Trim().ToUpperInvariant();
        if (normalizedStatus is not ("PENDING" or "CANCELLED" or "FAILED"))
            throw new InvalidArgumentException("A manager cannot manually set a payment to PAID or REFUNDED.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var order = await dbContext.YeuCauThanhToanVietQrs.SingleOrDefaultAsync(
            item => item.OrderId == orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("VietQR payment order was not found.");
        if (order.TrangThai is "PAID" or "REFUNDED")
            throw new InvalidArgumentException("A paid or refunded order cannot be changed manually.");
        if (order.TrangThai != "PENDING" && normalizedStatus != order.TrangThai)
            throw new InvalidArgumentException("Only a pending payment order can be changed manually.");

        order.TrangThai = normalizedStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToListResponse(order);
    }

    // Tính phần còn phải thu sau các thanh toán thành công và tiền cọc đã áp dụng.
    private async Task<decimal> GetRemainingBalanceAsync(HoaDonPhong invoice, CancellationToken cancellationToken)
    {
        var paid = await dbContext.ThanhToanHoaDonPhongs
            .Where(item => item.HoaDonPhongId == invoice.Id && item.TrangThai == "THANH_CONG")
            .SumAsync(item => (decimal?)item.SoTien, cancellationToken) ?? 0;
        var deposits = await dbContext.ApDungCocHoaDons
            .Where(item => item.HoaDonPhongId == invoice.Id)
            .SumAsync(item => (decimal?)item.SoTienApDung, cancellationToken) ?? 0;
        return invoice.TongTien - paid - deposits;
    }

    // Chuyển thời gian giao dịch provider thành UTC; nếu định dạng không hợp lệ thì dùng thời điểm đối soát.
    private static DateTime? ParseTransactionDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime)
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : null;

    private static VietQrPaymentResponse ToResponse(YeuCauThanhToanVietQr order)
        => new(order.OrderId, order.HoaDonPhongId, order.SoTien, order.NoiDung,
            order.QrCode, order.QrDataUrl, order.TrangThai, order.CreatedAt, order.PaidAt,
            order.MaGiaoDich, order.TransactionDateTime);

    // Chuyển entity thành DTO danh sách, không để lộ thông tin cấu hình VietQR.
    private static VietQrPaymentListResponse ToListResponse(YeuCauThanhToanVietQr order)
        => new(order.Id, order.OrderId, order.HoaDonPhongId, order.SoTien, order.NoiDung,
            order.QrCode, order.QrDataUrl, order.TrangThai, order.MaGiaoDich,
            order.GhiChu, order.CreatedAt, order.PaidAt, order.TransactionDateTime);
}
