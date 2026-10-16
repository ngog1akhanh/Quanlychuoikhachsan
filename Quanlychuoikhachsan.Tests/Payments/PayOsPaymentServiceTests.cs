using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS.Models.Webhooks;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Xunit;

namespace Quanlychuoikhachsan.Tests.Payments;

public sealed class PayOsPaymentServiceTests
{
    [Fact]
    public async Task CreatePayment_uses_remaining_invoice_balance_and_reuses_pending_order()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 1000, paid: 150, deposit: 50);
        var gateway = new FakePayOsGateway();
        var service = CreateService(db, gateway);

        var first = await service.CreateInvoicePaymentAsync(AccountId, InvoiceId, CancellationToken.None);
        var second = await service.CreateInvoicePaymentAsync(AccountId, InvoiceId, CancellationToken.None);

        Assert.Equal(800m, first.Amount);
        Assert.Equal("PENDING", first.Status);
        Assert.Equal(first.OrderCode, second.OrderCode);
        Assert.Equal(1, gateway.CreateCount);
        Assert.Equal(800, gateway.LastCreateRequest!.Amount);
        Assert.Equal(1, await db.YeuCauThanhToanPayOS.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_hides_invoice_owned_by_another_account()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 1000);
        var gateway = new FakePayOsGateway();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            CreateService(db, gateway).CreateInvoicePaymentAsync(AccountId + 1, InvoiceId, CancellationToken.None));

        Assert.Equal(0, gateway.CreateCount);
        Assert.Empty(await db.YeuCauThanhToanPayOS.ToListAsync());
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1000, 1000.5)]
    public async Task CreatePayment_rejects_zero_or_fractional_remaining_balance(decimal total, decimal paid)
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total, paid: paid);
        var gateway = new FakePayOsGateway();

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateService(db, gateway).CreateInvoicePaymentAsync(AccountId, InvoiceId, CancellationToken.None));

        Assert.Equal(0, gateway.CreateCount);
        Assert.Empty(await db.YeuCauThanhToanPayOS.ToListAsync());
    }

    [Fact]
    public async Task SuccessfulWebhook_marks_order_and_invoice_paid_once_when_replayed()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 800);
        await SeedOrderAsync(db, amount: 800);
        var gateway = new FakePayOsGateway
        {
            VerifiedWebhook = new PayOsVerifiedWebhook(OrderCode, 800, "00", "bank-ref-1")
        };
        var service = CreateService(db, gateway);
        var webhook = SuccessWebhook(OrderCode, 800);

        await service.ProcessWebhookAsync(webhook, CancellationToken.None);
        await service.ProcessWebhookAsync(webhook, CancellationToken.None);

        var order = await db.YeuCauThanhToanPayOS.SingleAsync();
        var invoice = await db.HoaDonPhongs.SingleAsync();
        Assert.Equal("PAID", order.TrangThai);
        Assert.Equal("bank-ref-1", order.MaGiaoDich);
        Assert.Equal("DA_THANH_TOAN", invoice.TrangThai);
        Assert.Equal(1, await db.ThanhToanHoaDonPhongs.CountAsync(x => x.CongThanhToan == "payOS"));
    }

    [Fact]
    public async Task Webhook_rejects_amount_mismatch_without_changing_payment_state()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 800);
        await SeedOrderAsync(db, amount: 800);
        var gateway = new FakePayOsGateway
        {
            VerifiedWebhook = new PayOsVerifiedWebhook(OrderCode, 799, "00", "bank-ref-1")
        };

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateService(db, gateway).ProcessWebhookAsync(SuccessWebhook(OrderCode, 799), CancellationToken.None));

        Assert.Equal("PENDING", (await db.YeuCauThanhToanPayOS.SingleAsync()).TrangThai);
        Assert.Empty(await db.ThanhToanHoaDonPhongs.ToListAsync());
    }

    [Fact]
    public async Task Webhook_rejects_invalid_signature()
    {
        await using var db = CreateDbContext();
        var gateway = new FakePayOsGateway { ThrowInvalidSignature = true };

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateService(db, gateway).ProcessWebhookAsync(SuccessWebhook(OrderCode, 800), CancellationToken.None));
    }

    [Fact]
    public async Task Non_success_webhook_leaves_order_pending()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 800);
        await SeedOrderAsync(db, amount: 800);
        var gateway = new FakePayOsGateway
        {
            VerifiedWebhook = new PayOsVerifiedWebhook(OrderCode, 800, "00", "bank-ref-1")
        };
        var webhook = SuccessWebhook(OrderCode, 800);
        webhook.Success = false;

        await CreateService(db, gateway).ProcessWebhookAsync(webhook, CancellationToken.None);

        Assert.Equal("PENDING", (await db.YeuCauThanhToanPayOS.SingleAsync()).TrangThai);
        Assert.Empty(await db.ThanhToanHoaDonPhongs.ToListAsync());
    }

    [Fact]
    public async Task Manual_status_update_cannot_set_paid_and_cancel_calls_gateway()
    {
        await using var db = CreateDbContext();
        await SeedInvoiceAsync(db, total: 800);
        await SeedOrderAsync(db, amount: 800);
        var gateway = new FakePayOsGateway();
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            service.UpdatePaymentStatusAsync(OrderCode, "PAID", CancellationToken.None));
        var cancelled = await service.UpdatePaymentStatusAsync(OrderCode, "CANCELLED", CancellationToken.None);

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal(1, gateway.CancelCount);
    }

    private const long AccountId = 500;
    private const long InvoiceId = 100;
    private const long BookingId = 700;
    private const long PaymentMethodId = 4;
    private const long PayOsPaymentMethodId = 5;
    private const long OrderCode = 2024001;

    private static HotelDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new HotelDbContext(options);
    }

    private static PayOsPaymentService CreateService(HotelDbContext db, IPayOsGateway gateway)
        => new(db, gateway, NullLogger<PayOsPaymentService>.Instance);

    private static async Task SeedInvoiceAsync(HotelDbContext db, decimal total, decimal paid = 0, decimal deposit = 0)
    {
        var now = DateTime.UtcNow;
        db.TaiKhoans.Add(new TaiKhoan
        {
            Id = AccountId,
            MatKhauHash = "test-hash",
            LoaiTaiKhoan = "KHACH_HANG",
            CreatedAt = now,
            UpdatedAt = now
        });
        var customer = new KhachHang
        {
            Id = 600,
            MaKhachHang = "KH-TEST",
            HoTen = "Guest",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.KhachHangs.Add(customer);
        db.PhuongThucThanhToans.AddRange(
            new PhuongThucThanhToan { Id = PaymentMethodId, MaPhuongThuc = "CASH", TenPhuongThuc = "Cash" },
            new PhuongThucThanhToan { Id = PayOsPaymentMethodId, MaPhuongThuc = "PAYOS", TenPhuongThuc = "payOS QR" });
        db.DatPhongs.Add(new DatPhong
        {
            Id = BookingId,
            MaDatPhong = "DP-TEST",
            KhachHangId = customer.Id,
            TaiKhoanTaoId = AccountId,
            NgayDat = now,
            DuKienNhan = now.AddDays(1),
            DuKienTra = now.AddDays(2),
            SoNguoiLon = 1,
            KhachHang = customer
        });
        db.HoaDonPhongs.Add(new HoaDonPhong
        {
            Id = InvoiceId,
            MaHoaDon = "HD-TEST",
            DatPhongId = BookingId,
            NgayLap = now,
            TongTien = total,
            TrangThai = "DA_PHAT_HANH"
        });

        if (paid > 0)
        {
            db.ThanhToanHoaDonPhongs.Add(new ThanhToanHoaDonPhong
            {
                Id = 800,
                MaThanhToan = "TT-CASH-TEST",
                HoaDonPhongId = InvoiceId,
                PhuongThucId = PaymentMethodId,
                SoTien = paid,
                ThoiGianThanhToan = now,
                TrangThai = "THANH_CONG"
            });
        }

        if (deposit > 0)
        {
            db.PhieuCocs.Add(new PhieuCoc
            {
                Id = 900,
                MaPhieuCoc = "PC-TEST",
                DatPhongId = BookingId,
                PhuongThucId = PaymentMethodId,
                SoTien = deposit,
                ThoiGianCoc = now,
                TrangThai = "DA_THANH_TOAN"
            });
            db.ApDungCocHoaDons.Add(new ApDungCocHoaDon
            {
                HoaDonPhongId = InvoiceId,
                PhieuCocId = 900,
                SoTienApDung = deposit
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedOrderAsync(HotelDbContext db, decimal amount)
    {
        var now = DateTime.UtcNow;
        db.YeuCauThanhToanPayOS.Add(new YeuCauThanhToanPayOS
        {
            OrderCode = OrderCode,
            HoaDonPhongId = InvoiceId,
            SoTien = amount,
            NoiDung = $"HD-{OrderCode}",
            CheckoutUrl = "https://payos.example/checkout",
            QrCode = "000201",
            TrangThai = "PENDING",
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    private static Webhook SuccessWebhook(long orderCode, int amount) => new()
    {
        Code = "00",
        Description = "success",
        Success = true,
        Data = new WebhookData
        {
            OrderCode = orderCode,
            Amount = amount,
            Code = "00",
            Reference = "bank-ref-1"
        },
        Signature = "test-signature"
    };

    private sealed class FakePayOsGateway : IPayOsGateway
    {
        public int CreateCount { get; private set; }
        public int CancelCount { get; private set; }
        public bool ThrowInvalidSignature { get; init; }
        public PayOsPaymentLinkRequest? LastCreateRequest { get; private set; }
        public PayOsVerifiedWebhook VerifiedWebhook { get; init; } = new(OrderCode, 800, "00", "bank-ref-1");

        public Task<PayOsPaymentLinkResult> CreatePaymentLinkAsync(
            PayOsPaymentLinkRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCount++;
            LastCreateRequest = request;
            return Task.FromResult(new PayOsPaymentLinkResult(
                request.OrderCode,
                "https://payos.example/checkout",
                "000201",
                "pay-link-id"));
        }

        public Task<PayOsVerifiedWebhook> VerifyWebhookAsync(Webhook webhook, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowInvalidSignature)
                throw new InvalidPayOsWebhookSignatureException();
            return Task.FromResult(VerifiedWebhook);
        }

        public Task CancelPaymentLinkAsync(long orderCode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelCount++;
            return Task.CompletedTask;
        }
    }
}
