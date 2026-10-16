using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Inventory;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Điều phối danh mục hàng hóa, nhập xuất kho và truy vấn số dư tồn.</summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class InventoryController(IInventoryService service) : ApiControllerBase
{
    [HttpGet("inventory-items")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>> Items(CancellationToken ct) => OkResponse(await service.ListItemsAsync(ct));

    [HttpPost("inventory-items")]
    [ProducesResponseType<ApiResponse<InventoryItemResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> CreateItem(CreateInventoryItemRequest request, CancellationToken ct) => Created($"/v1/inventory-items", await service.CreateItemAsync(request, ct));

    [HttpPost("purchase-orders")]
    [ProducesResponseType<ApiResponse<PurchaseResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<PurchaseResponse>>> CreatePurchase(CreatePurchaseRequest request, CancellationToken ct) => Created($"/v1/purchase-orders", await service.CreatePurchaseAsync(request, ct));

    [HttpPost("purchase-orders/{id:long}:confirm")]
    public async Task<ActionResult<PurchaseResponse>> ConfirmPurchase(long id, ConfirmPurchaseRequest request, CancellationToken ct) => OkResponse(await service.ConfirmPurchaseAsync(id, request, ct));

    [HttpPost("stock-issues")]
    [ProducesResponseType<ApiResponse<IssueResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<IssueResponse>>> CreateIssue(CreateIssueRequest request, CancellationToken ct) => Created($"/v1/stock-issues", await service.CreateIssueAsync(request, ct));

    [HttpPost("stock-issues/{id:long}:confirm")]
    public async Task<ActionResult<IssueResponse>> ConfirmIssue(long id, CancellationToken ct) => OkResponse(await service.ConfirmIssueAsync(id, ct));

    [HttpGet("warehouses/{warehouseId:long}/inventory-items/{itemId:long}/balance")]
    public async Task<ActionResult<StockBalanceResponse>> Balance(long warehouseId, long itemId, CancellationToken ct) => OkResponse(await service.GetBalanceAsync(warehouseId, itemId, ct));
}