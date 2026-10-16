using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Housekeeping;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Quản lý công việc buồng phòng và ghi nhận tình trạng phòng.</summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class HousekeepingController(IHousekeepingService service) : ApiControllerBase
{
    [HttpGet("housekeeping-tasks")]
    public async Task<ActionResult<IReadOnlyList<HousekeepingTaskResponse>>> List(CancellationToken ct) => OkResponse(await service.ListTasksAsync(ct));

    [HttpPost("housekeeping-tasks/checkout")]
    [ProducesResponseType<ApiResponse<HousekeepingTaskResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<HousekeepingTaskResponse>>> Checkout(CheckoutRequest request, CancellationToken ct) => Created("/v1/housekeeping-tasks", await service.CreateCheckoutTaskAsync(request, ct));

    [HttpPost("room-conditions")]
    [ProducesResponseType<ApiResponse<RoomConditionResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<RoomConditionResponse>>> Record(CreateRoomConditionRequest request, CancellationToken ct) => Created("/v1/room-conditions", await service.RecordConditionAsync(request, ct));
}
