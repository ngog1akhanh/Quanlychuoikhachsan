using QuanLyKhachSan.Web.Models.Booking;
using QuanLyKhachSan.Web.Models.Payment;

namespace QuanLyKhachSan.Web.Services;

/// <summary>Phương thức thanh toán, xác nhận cọc, VietQR.</summary>
public sealed class PaymentService
{
    private readonly ApiClient _api;

    public PaymentService(ApiClient api) => _api = api;

    /// <summary>Đang chạy bằng dữ liệu mẫu? (Checkout dùng để chọn giữa cọc DEMO và cọc VietQR thật)</summary>
    public bool UseMock => _api.UseMock;

    /// <summary>Khách đặt cọc online chỉ có VietQR; danh mục phương thức đầy đủ dành cho thu ngân.</summary>
    public async Task<List<PaymentMethodItem>> GetMethodsAsync()
    {
        if (_api.UseMock) { await Task.Delay(200); return MockData.PaymentMethods; }
        return new List<PaymentMethodItem> { new(0, "VIETQR", "Chuyển khoản VietQR") };
    }

    /// <summary>POST /v1/bookings/{id}/deposit-payment – tạo (hoặc lấy lại) đơn cọc VietQR của booking đang giữ chỗ.</summary>
    public Task<DepositOrder> CreateDepositOrderAsync(long bookingId)
        => _api.PostAsync<DepositOrder>($"v1/bookings/{bookingId}/deposit-payment");

    /// <summary>POST /v1/deposit-payments/{orderId}/inquiry – đối chiếu với ngân hàng, nếu đã nhận tiền thì booking chuyển BOOKED.</summary>
    public Task<DepositOrder> InquireDepositAsync(string orderId)
        => _api.PostAsync<DepositOrder>($"v1/deposit-payments/{Uri.EscapeDataString(orderId)}/inquiry");

    /// <summary>
    /// Chỉ dùng ở chế độ mock. Với API thật, route :confirm-deposit dành cho nhân viên thu ngân (BILLING_PAYMENT_WRITE);
    /// khách dùng CreateDepositOrderAsync + InquireDepositAsync.
    /// </summary>
    public async Task<BookingResponse> ConfirmDepositAsync(BookingResponse booking, PaymentMethodItem method, decimal amount)
    {
        var txn = $"DEMO-{booking.BookingCode}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        if (_api.UseMock)
        {
            await Task.Delay(700);
            lock (MockData.Bookings)
            {
                var idx = MockData.Bookings.FindIndex(b => b.Id == booking.Id);
                var updated = booking with { Status = BookingStatus.Booked, HoldExpiresAt = null };
                if (idx >= 0) MockData.Bookings[idx] = updated;
                return updated;
            }
        }

        throw new ApiException("Khách hàng đặt cọc qua VietQR (CreateDepositOrderAsync), không dùng confirm-deposit của thu ngân.");
    }

    /// <summary>POST /v1/invoices/{invoiceId}/vietqr-payment – QR cho hóa đơn (sau khi có hóa đơn).</summary>
    public async Task<VietQrPaymentResponse> CreateVietQrAsync(long invoiceId)
    {
        var res = await _api.PostAsync<PaymentApiResponse<VietQrPaymentResponse>>($"v1/invoices/{invoiceId}/vietqr-payment");
        return res.Data ?? throw new ApiException(res.Message);
    }

    /// <summary>POST /v1/payments/vietqr/{orderId}/inquiry – kiểm tra trạng thái đơn QR.</summary>
    public async Task<VietQrPaymentResponse> InquireVietQrAsync(string orderId)
    {
        var res = await _api.PostAsync<PaymentApiResponse<VietQrPaymentResponse>>($"v1/payments/vietqr/{orderId}/inquiry");
        return res.Data ?? throw new ApiException(res.Message);
    }
}
