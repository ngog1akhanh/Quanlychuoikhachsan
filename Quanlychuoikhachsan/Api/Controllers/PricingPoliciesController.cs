using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record PricingLine(long RoomTypeId, string AdjustmentType, decimal Value);
public sealed record PricingRequest([param: Required, StringLength(40)] string Code, [param: Required, StringLength(150)] string Name,
    DateOnly From, DateOnly To, int Priority, [param: Required, MinLength(1), MaxLength(7)] IReadOnlyList<byte> Weekdays,
    [param: Required, MinLength(1), MaxLength(100)] IReadOnlyList<PricingLine> Lines, [param: StringLength(1000)] string? Description, string Status = "HOAT_DONG");
public sealed record DepositPolicyRequest([param: Required, StringLength(150)] string Name, string Type, decimal Value, DateOnly? From, DateOnly? To, string Status = "HOAT_DONG");
public sealed record CancellationRule(int MinimumHours, decimal RefundPercent, decimal FixedFee);
public sealed record CancellationPolicyRequest([param: Required, StringLength(150)] string Name, [param: StringLength(1000)] string? Description,
    [param: Required, MinLength(1), MaxLength(100)] IReadOnlyList<CancellationRule> Rules, string Status = "HOAT_DONG");
public sealed record SurchargePolicyRequest([param: Required, StringLength(150)] string Name, string Type, int FromMinute, int? ToMinute, string Calculation, decimal Value, string Status = "HOAT_DONG");

[ApiController, Authorize, Route("v1"), RequirePermission("PRICING_WRITE")]
public sealed class PricingPoliciesController(HotelDbContext db) : ControllerBase
{
    [HttpGet("pricing-policies")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.ChinhSachGias.AsNoTracking().Include(x => x.CacThuApDung).Include(x => x.ChiTiet).OrderByDescending(x => x.Id).ToListAsync(ct));
    [HttpPost("pricing-policies")]
    public Task<IActionResult> Create(PricingRequest r, CancellationToken ct) => Save(null, r, ct);
    [HttpPut("pricing-policies/{id:long}")]
    public Task<IActionResult> Update(long id, PricingRequest r, CancellationToken ct) => Save(id, r, ct);
    private async Task<IActionResult> Save(long? id, PricingRequest r, CancellationToken ct)
    {
        Input.Range(r.From, r.To); Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        if (r.Weekdays.Any(x => x is < 1 or > 7) || r.Weekdays.Distinct().Count() != r.Weekdays.Count || r.Lines.Select(x => x.RoomTypeId).Distinct().Count() != r.Lines.Count) throw new InvalidArgumentException("Weekdays and room types must be valid and unique.");
        foreach (var line in r.Lines) { Input.OneOf(line.AdjustmentType, "GIA_CO_DINH", "TANG_%", "GIAM_%", "TANG_TIEN", "GIAM_TIEN"); Input.Money(line.Value, "value"); if (line.AdjustmentType == "GIAM_%" && line.Value > 100) throw new InvalidArgumentException("Discount percentage exceeds 100."); }
        var ids = r.Lines.Select(x => x.RoomTypeId).ToArray(); if (await db.LoaiPhongs.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) throw new InvalidArgumentException("Unknown room type.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var x = id.HasValue ? await db.ChinhSachGias.Include(x => x.ChiTiet).Include(x => x.CacThuApDung).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Pricing policy was not found.") : new ChinhSachGia();
        if (id.HasValue) { db.RemoveRange(x.ChiTiet); db.RemoveRange(x.CacThuApDung); await db.SaveChangesAsync(ct); } else db.Add(x);
        x.MaChinhSach = r.Code; x.TenChinhSach = r.Name; x.TuNgay = r.From; x.DenNgay = r.To; x.MucUuTien = r.Priority; x.MoTa = r.Description; x.TrangThai = r.Status;
        x.ChiTiet = r.Lines.Select(l => new ChiTietChinhSachGia { LoaiPhongId = l.RoomTypeId, KieuDieuChinh = l.AdjustmentType, GiaTri = l.Value }).ToList();
        x.CacThuApDung = r.Weekdays.Select(d => new ChinhSachGiaThu { Thu = d }).ToList();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { x.Id });
    }
    [HttpGet("deposit-policies")]
    public async Task<IActionResult> Deposits(CancellationToken ct) => Ok(await db.Set<ChinhSachCoc>().AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct));
    [HttpPost("deposit-policies")]
    public async Task<IActionResult> Deposit(DepositPolicyRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Type, "PHAN_TRAM", "DEM_DAU", "SO_TIEN_CO_DINH"); Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Money(r.Value, "value");
        if (r.To < r.From || (r.Type == "PHAN_TRAM" && r.Value > 100)) throw new InvalidArgumentException("Invalid deposit policy.");
        var x = new ChinhSachCoc { TenChinhSach = r.Name, KieuTinh = r.Type, GiaTri = r.Value, TuNgay = r.From, DenNgay = r.To, TrangThai = r.Status }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/deposit-policies/{x.Id}", x);
    }
    [HttpGet("cancellation-policies"), AllowAnonymous]
    public async Task<IActionResult> Cancellations(CancellationToken ct) => Ok(await db.Set<ChinhSachHuy>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").Select(x => new { x.Id, x.TenChinhSach, x.MoTa, Rules = db.Set<ChiTietChinhSachHuy>().Where(d => d.ChinhSachHuyId == x.Id).OrderByDescending(d => d.SoGioTruocToiThieu).ToList() }).ToListAsync(ct));
    [HttpPost("cancellation-policies")]
    public async Task<IActionResult> Cancellation(CancellationPolicyRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG");
        if (r.Rules.Select(x => x.MinimumHours).Distinct().Count() != r.Rules.Count) throw new InvalidArgumentException("Cancellation thresholds must be unique.");
        foreach (var line in r.Rules) { if (line.MinimumHours < 0 || line.RefundPercent is < 0 or > 100) throw new InvalidArgumentException("Invalid cancellation rule."); Input.Money(line.RefundPercent, "refund_percent"); Input.Money(line.FixedFee, "fixed_fee"); }
        await using var tx = await db.Database.BeginTransactionAsync(ct); var x = new ChinhSachHuy { TenChinhSach = r.Name, MoTa = r.Description, TrangThai = r.Status }; db.Add(x); await db.SaveChangesAsync(ct);
        db.AddRange(r.Rules.Select(d => new ChiTietChinhSachHuy { ChinhSachHuyId = x.Id, SoGioTruocToiThieu = d.MinimumHours, PhanTramHoan = d.RefundPercent, PhiHuyCoDinh = d.FixedFee })); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Created($"/v1/cancellation-policies/{x.Id}", new { x.Id });
    }
    [HttpGet("surcharge-policies")]
    public async Task<IActionResult> Surcharges(CancellationToken ct) => Ok(await db.Set<ChinhSachPhuThu>().AsNoTracking().OrderBy(x => x.TuPhut).ToListAsync(ct));
    [HttpPost("surcharge-policies")]
    public async Task<IActionResult> Surcharge(SurchargePolicyRequest r, CancellationToken ct)
    {
        Input.OneOf(r.Type, "NHAN_SOM", "TRA_TRE"); Input.OneOf(r.Calculation, "PHAN_TRAM", "SO_TIEN_CO_DINH"); Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); Input.Money(r.Value, "value");
        if (r.FromMinute < 0 || r.ToMinute < r.FromMinute) throw new InvalidArgumentException("Invalid minute range.");
        var x = new ChinhSachPhuThu { TenChinhSach = r.Name, Loai = r.Type, TuPhut = r.FromMinute, DenPhut = r.ToMinute, KieuTinh = r.Calculation, GiaTri = r.Value, TrangThai = r.Status }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/surcharge-policies/{x.Id}", x);
    }
    [HttpPatch("deposit-policies/{id:long}/status")]
    public async Task<IActionResult> DepositStatus(long id, StatusRequest r, CancellationToken ct) { Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); var x = await db.Set<ChinhSachCoc>().FindAsync([id], ct) ?? throw new ResourceNotFoundException("Policy was not found."); x.TrangThai = r.Status; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpPatch("cancellation-policies/{id:long}/status")]
    public async Task<IActionResult> CancelStatus(long id, StatusRequest r, CancellationToken ct) { Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); var x = await db.Set<ChinhSachHuy>().FindAsync([id], ct) ?? throw new ResourceNotFoundException("Policy was not found."); x.TrangThai = r.Status; await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpPatch("surcharge-policies/{id:long}/status")]
    public async Task<IActionResult> SurchargeStatus(long id, StatusRequest r, CancellationToken ct) { Input.OneOf(r.Status, "HOAT_DONG", "NGUNG_HOAT_DONG"); var x = await db.Set<ChinhSachPhuThu>().FindAsync([id], ct) ?? throw new ResourceNotFoundException("Policy was not found."); x.TrangThai = r.Status; await db.SaveChangesAsync(ct); return NoContent(); }
}
