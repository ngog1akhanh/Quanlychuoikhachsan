using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tra cứu và tạo loại phòng cùng giới hạn sức chứa và giá cơ bản.</summary>
[Route("v1/room-types")]
public sealed class RoomTypesController(HotelDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("ROOM_TYPE_READ")]
    public async Task<ActionResult<IReadOnlyList<RoomTypeResponse>>> List(CancellationToken cancellationToken)
    {
        var types = await dbContext.LoaiPhongs.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new RoomTypeResponse(x.Id, x.MaLoaiPhong, x.TenLoaiPhong, x.SoNguoiLonToiDa, x.SoTreEmToiDa, x.GiaCoBanDem, x.TrangThai))
            .ToListAsync(cancellationToken);
        return OkResponse<IReadOnlyList<RoomTypeResponse>>(types);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("ROOM_TYPE_READ")]
    public async Task<ActionResult<RoomTypeResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var type = await dbContext.LoaiPhongs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Room type room-types/{id} was not found.");
        return OkResponse(new RoomTypeResponse(type.Id, type.MaLoaiPhong, type.TenLoaiPhong, type.SoNguoiLonToiDa, type.SoTreEmToiDa, type.GiaCoBanDem, type.TrangThai));
    }

    [HttpPost]
    [RequirePermission("ROOM_TYPE_WRITE")]
    [ProducesResponseType<ApiResponse<RoomTypeResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<RoomTypeResponse>>> Create(CreateRoomTypeRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxAdults < 1 || request.MaxChildren < 0 || request.BaseNightlyPrice < 0)
            throw new InvalidArgumentException("Room capacity and price values are invalid.");
        if (await dbContext.LoaiPhongs.AnyAsync(x => x.MaLoaiPhong == request.Code, cancellationToken))
            throw new AlreadyExistsException("The room type code already exists.");
        var type = new LoaiPhong
        {
            MaLoaiPhong = request.Code.Trim(),
            TenLoaiPhong = request.Name.Trim(),
            SoNguoiLonToiDa = request.MaxAdults,
            SoTreEmToiDa = request.MaxChildren,
            GiaCoBanDem = request.BaseNightlyPrice,
            MoTa = request.Description,
            TrangThai = "HOAT_DONG"
        };
        dbContext.LoaiPhongs.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/v1/room-types/{type.Id}", new RoomTypeResponse(type.Id, type.MaLoaiPhong, type.TenLoaiPhong, type.SoNguoiLonToiDa, type.SoTreEmToiDa, type.GiaCoBanDem, type.TrangThai));
    }
}
