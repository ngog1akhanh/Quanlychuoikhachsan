using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Application.Rooms;
using Quanlychuoikhachsan.Infrastructure.Authentication;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>
/// API quản lý phòng cho App quản lý (Giai đoạn 1, mục 9): sơ đồ phòng, loại phòng, sửa phòng,
/// đổi trạng thái vận hành, lịch phòng. Bổ sung cho RoomsController/RoomTypesController có sẵn.
/// </summary>
[ApiController]
[Authorize]
[Route("v1")]
public sealed class RoomAdminController(IRoomAdminService rooms) : ApiControllerBase
{
    [HttpGet("room-board")]
    [RequirePermission("ROOM_READ")]
    public async Task<ActionResult<IReadOnlyList<RoomBoardItem>>> Board(CancellationToken ct)
        => OkResponse(await rooms.GetBoardAsync(ct));

    [HttpGet("room-board/types")]
    [RequirePermission("ROOM_TYPE_READ")]
    public async Task<ActionResult<IReadOnlyList<RoomTypeSummary>>> Types(CancellationToken ct)
        => OkResponse(await rooms.GetRoomTypesAsync(ct));

    [HttpGet("room-calendar")]
    [RequirePermission("ROOM_READ")]
    public async Task<ActionResult<RoomCalendar>> Calendar([FromQuery(Name = "from")] DateOnly fromDate, [FromQuery(Name = "to")] DateOnly toDate, CancellationToken ct)
        => OkResponse(await rooms.GetCalendarAsync(fromDate, toDate, ct));

    [HttpPatch("rooms/{id:long}")]
    [RequirePermission("ROOM_WRITE")]
    public async Task<ActionResult<RoomBoardItem>> UpdateRoom(long id, UpdateRoomRequest request, CancellationToken ct)
        => OkResponse(await rooms.UpdateRoomAsync(id, request, ct));

    [HttpPost("rooms/{id:long}:set-status")]
    [RequirePermission("ROOM_WRITE")]
    public async Task<ActionResult<RoomBoardItem>> SetStatus(long id, SetRoomStatusRequest request, CancellationToken ct)
        => OkResponse(await rooms.SetStatusAsync(id, request, ct));

    [HttpPatch("room-types/{id:long}")]
    [RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<ActionResult<RoomTypeSummary>> UpdateRoomType(long id, UpdateRoomTypeRequest request, CancellationToken ct)
        => OkResponse(await rooms.UpdateRoomTypeAsync(id, request, ct));
}
