using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

[ApiController, Authorize, Route("v1")]
public sealed class DepositPaymentsController(DepositPaymentService service, HotelDbContext db, CurrentActor actor, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>Khách tạo (hoặc lấy lại) QR cọc cho booking HOLD của mình.</summary>
    [HttpPost("bookings/{id:long}/deposit-payment")]
    public async Task<IActionResult> Create(long id, CancellationToken ct) => Ok(await service.ViewAsync(await service.CreateAsync(id, ct), ct));

    /// <summary>Tra cứu VietQR ngay (khách chủ đơn hoặc nhân viên BILLING_PAYMENT_WRITE).</summary>
    [HttpPost("deposit-payments/{orderId}/inquiry")]
    public async Task<IActionResult> Inquire(string orderId, CancellationToken ct) => Ok(await service.ViewAsync(await service.InquireAsync(orderId, ct), ct));

    /// <summary>Chỉ môi trường Development + chế độ demo VietQR: giả lập ngân hàng báo đã nhận đủ tiền.</summary>
    [HttpPost("deposit-payments/{orderId}:simulate-paid")]
    public async Task<IActionResult> SimulatePaid(string orderId, CancellationToken ct)
    {
        if (!env.IsDevelopment()) throw new ResourceNotFoundException("Simulated payment is not available.");
        return Ok(await service.ViewAsync(await service.SimulatePaidAsync(orderId, ct), ct));
    }

    [HttpGet("deposit-payments")]
    public async Task<IActionResult> List([FromQuery] ListQuery q, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BILLING_READ", ct); return Ok(await db.Set<DepositPaymentOrder>().AsNoTracking().Where(x => (staff || x.AccountId == actor.AccountId) && (q.Status == null || x.Status == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
}
