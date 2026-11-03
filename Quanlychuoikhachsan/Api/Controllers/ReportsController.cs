using Quanlychuoikhachsan.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Reports;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Cung cấp báo cáo tổng quan, doanh thu và công suất phòng theo khoảng ngày bao gồm cả hai đầu.</summary>
[ApiController]
[Authorize]
[Route("v1/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("dashboard")]
    [RequirePermission("REPORT_READ")]
    public async Task<ActionResult<DashboardResponse>> Dashboard([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => OkResponse(await reports.DashboardAsync(Validate(from, to), ct));

    [HttpGet("revenue")]
    [RequirePermission("REPORT_READ")]
    public async Task<ActionResult<RevenueReportResponse>> Revenue([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => OkResponse(await reports.RevenueAsync(Validate(from, to), ct));

    [HttpGet("occupancy")]
    [RequirePermission("REPORT_READ")]
    public async Task<ActionResult<OccupancyReportResponse>> Occupancy([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => OkResponse(await reports.OccupancyAsync(Validate(from, to), ct));

    private static ReportRange Validate(DateOnly from, DateOnly to)
    {
        Application.Common.Input.Range(from, to);
        return new(from, to);
    }
}
