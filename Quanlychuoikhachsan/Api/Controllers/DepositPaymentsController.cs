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
public sealed class DepositPaymentsController(DepositPaymentService service, HotelDbContext db, CurrentActor actor) : ControllerBase
{
    [HttpPost("bookings/{id:long}/deposit-payment")]
    public async Task<IActionResult> Create(long id, CancellationToken ct) => Ok(await service.CreateAsync(id, ct));
    [HttpPost("deposit-payments/{orderId}/inquiry")]
    public async Task<IActionResult> Inquire(string orderId, CancellationToken ct) => Ok(await service.InquireAsync(orderId, ct));
    [HttpGet("deposit-payments")]
    public async Task<IActionResult> List([FromQuery] ListQuery q, CancellationToken ct)
    {
        var staff = await actor.HasPermissionAsync("BILLING_READ", ct); return Ok(await db.Set<DepositPaymentOrder>().AsNoTracking().Where(x => (staff || x.AccountId == actor.AccountId) && (q.Status == null || x.Status == q.Status)).OrderByDescending(x => x.Id).PageAsync(q, ct));
    }
}
