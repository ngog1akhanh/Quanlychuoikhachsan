using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tra cứu phòng, xem trước giá lưu trú và tạo phòng theo quyền tương ứng.</summary>
[Route("v1/rooms")]
public sealed class RoomsController(HotelDbContext dbContext, IBookingService bookingService) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("ROOM_READ")]
    public async Task<ActionResult<IReadOnlyList<RoomResponse>>> List(CancellationToken cancellationToken)
    {
        var rooms = await dbContext.Phongs.AsNoTracking().Include(x => x.LoaiPhong).OrderBy(x => x.Id)
            .Select(x => new RoomResponse(x.Id, $"rooms/{x.Id}", x.MaPhong, x.SoPhong, x.LoaiPhongId, x.LoaiPhong.MaLoaiPhong, x.TrangThai))
            .ToListAsync(cancellationToken);
        return OkResponse<IReadOnlyList<RoomResponse>>(rooms);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("ROOM_READ")]
    public async Task<ActionResult<RoomResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var room = await dbContext.Phongs.AsNoTracking().Include(x => x.LoaiPhong).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Room rooms/{id} was not found.");
        return OkResponse(new RoomResponse(room.Id, $"rooms/{room.Id}", room.MaPhong, room.SoPhong, room.LoaiPhongId, room.LoaiPhong.MaLoaiPhong, room.TrangThai));
    }

    [HttpGet("{id:long}/price")]
    [RequirePermission("ROOM_READ")]
    public Task<ActionResult<PricePreviewResponse>> PreviewPrice(long id, [FromQuery] DateOnly checkInDate, [FromQuery] DateOnly checkOutDate, CancellationToken cancellationToken)
        => PreviewPriceCoreAsync(id, checkInDate, checkOutDate, cancellationToken);

    [HttpPost]
    [RequirePermission("ROOM_WRITE")]
    [ProducesResponseType<ApiResponse<RoomResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<RoomResponse>>> Create(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (request.DefaultNightSurcharge < 0)
            throw new InvalidArgumentException("default_night_surcharge cannot be negative.");
        var roomType = await dbContext.LoaiPhongs.FindAsync([request.RoomTypeId], cancellationToken)
            ?? throw new ResourceNotFoundException($"Room type room-types/{request.RoomTypeId} was not found.");
        if (await dbContext.Phongs.AnyAsync(x => x.MaPhong == request.RoomCode || x.SoPhong == request.RoomNumber, cancellationToken))
            throw new AlreadyExistsException("The room code or room number already exists.");
        var room = new Phong
        {
            MaPhong = request.RoomCode.Trim(),
            SoPhong = request.RoomNumber.Trim(),
            LoaiPhongId = request.RoomTypeId,
            Tang = request.Floor,
            LoaiView = request.ViewType,
            PhuThuDemMacDinh = request.DefaultNightSurcharge,
            TrangThai = "SAN_SANG"
        };
        dbContext.Phongs.Add(room);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/v1/rooms/{room.Id}", new RoomResponse(room.Id, $"rooms/{room.Id}", room.MaPhong, room.SoPhong, room.LoaiPhongId, roomType.MaLoaiPhong, room.TrangThai));
    }

    private async Task<ActionResult<PricePreviewResponse>> PreviewPriceCoreAsync(long id, DateOnly checkInDate, DateOnly checkOutDate, CancellationToken cancellationToken)
        => OkResponse(await bookingService.PreviewPriceAsync(id, checkInDate, checkOutDate, cancellationToken));
}
