using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Reports;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Thống kê hóa đơn theo kỳ, theo nhân viên và theo sản phẩm (tab "Thống kê hóa đơn" trong App).</summary>
[ApiController, Authorize, RequirePermission("REPORT_READ"), Route("v1/reports/invoice-stats")]
public sealed class InvoiceStatsController(IInvoiceStatsService stats) : ControllerBase
{
    /// <summary>Tổng tiền hóa đơn theo tháng / quý / năm. "Các năm" = groupBy=year với nhiều năm.</summary>
    [HttpGet("periods")]
    public async Task<IActionResult> Periods(int fromYear, int toYear, string groupBy = "month", CancellationToken ct = default)
        => Ok(await stats.ByPeriodAsync(fromYear, toYear, groupBy, ct));

    /// <summary>Tổng tiền hóa đơn theo nhân viên lập, theo quý hoặc năm.</summary>
    [HttpGet("staff")]
    public async Task<IActionResult> Staff(int fromYear, int toYear, string groupBy = "quarter", CancellationToken ct = default)
        => Ok(await stats.ByStaffAsync(fromYear, toYear, groupBy, ct));

    /// <summary>Doanh thu theo sản phẩm (dịch vụ, loại phòng) theo từng năm.</summary>
    [HttpGet("products")]
    public async Task<IActionResult> Products(int fromYear, int toYear, CancellationToken ct = default)
        => Ok(await stats.ByProductAsync(fromYear, toYear, ct));
}
