using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Quanlychuoikhachsan.Domain.Entities;

[Table("vietqr_deposit_order"), Index(nameof(OrderId), IsUnique = true), Index(nameof(BookingId), IsUnique = true), Index(nameof(TransactionId), IsUnique = true)]
public sealed class DepositPaymentOrder : EntityBase
{
    [Column("order_id"), MaxLength(80)] public string OrderId { get; set; } = null!;
    [Column("booking_id")] public long BookingId { get; set; }
    [Column("account_id")] public long AccountId { get; set; }
    [Column("amount"), Precision(15, 2)] public decimal Amount { get; set; }
    [Column("description"), MaxLength(100)] public string Description { get; set; } = null!;
    [Column("qr_code")] public string QrCode { get; set; } = null!;
    [Column("qr_data_url")] public string QrDataUrl { get; set; } = null!;
    [Column("status"), MaxLength(30)] public string Status { get; set; } = "PENDING";
    [Column("transaction_id"), MaxLength(150)] public string? TransactionId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("paid_at")] public DateTime? PaidAt { get; set; }
}
