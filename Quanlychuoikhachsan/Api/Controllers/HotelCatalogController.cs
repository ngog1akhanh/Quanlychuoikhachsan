using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Bookings;
using Quanlychuoikhachsan.Application.Rooms;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;
public sealed record HotelRequest([param: Required, StringLength(150)] string Name, [param: Required, StringLength(255)] string Address,
    [param: StringLength(30)] string? Phone, [param: EmailAddress, StringLength(150)] string? Email, [param: Url, StringLength(255)] string? Website,
    TimeOnly? CheckInTime, TimeOnly? CheckOutTime, [param: StringLength(10000)] string? Description);
public sealed record RoomUpdateRequest(long RoomTypeId, [param: Required, StringLength(20)] string Number, [param: StringLength(20)] string? Floor,
    [param: StringLength(80)] string? Direction, [param: StringLength(100)] string? View, [param: StringLength(150)] string? Location,
    decimal Surcharge, [param: StringLength(1000)] string? Description, [param: StringLength(500)] string? Note);
public sealed record RoomTypeUpdateRequest([param: Required, StringLength(100)] string Name, int MaxAdults, int MaxChildren, decimal? Area,
    decimal BasePrice, [param: StringLength(10000)] string? Description, string Status);
public sealed record ImageRequest([param: Required, Url, StringLength(1000)] string Url, [param: StringLength(255)] string? AltText, bool Cover, int Order);
public sealed record AmenityRequest([param: Required, StringLength(30)] string Code, [param: Required, StringLength(120)] string Name,
    [param: StringLength(255)] string? Icon, [param: StringLength(500)] string? Description);

[ApiController, Authorize, Route("v1")]
public sealed class HotelCatalogController(HotelDbContext db, IBookingService bookings, IRoomAdminService rooms) : ControllerBase
{
    [AllowAnonymous, HttpGet("hotels")]
    public async Task<IActionResult> Hotels(CancellationToken ct) => Ok(await db.Set<KhachSan>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").ToListAsync(ct));
    [HttpPost("hotels"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Hotel(HotelRequest r, CancellationToken ct) { var x = new KhachSan(); Apply(x, r); db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/hotels/{x.Id}", x); }
    [HttpPut("hotels/{id:long}"), RequirePermission("HOTEL_WRITE")]
    public async Task<IActionResult> Hotel(long id, HotelRequest r, CancellationToken ct) { var x = await db.Set<KhachSan>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Hotel was not found."); Apply(x, r); await db.SaveChangesAsync(ct); return Ok(x); }
    private static void Apply(KhachSan x, HotelRequest r) { x.TenKhachSan = r.Name; x.DiaChi = r.Address; x.SoDienThoai = r.Phone; x.Email = r.Email; x.Website = r.Website; x.GioNhanPhong = r.CheckInTime; x.GioTraPhong = r.CheckOutTime; x.MoTa = r.Description; }

    [AllowAnonymous, HttpGet("public/rooms/availability")]
    public async Task<IActionResult> Available([FromQuery] RoomAvailabilityQuery q, long? roomTypeId, string? view, decimal? minPrice, decimal? maxPrice, CancellationToken ct)
    {
        if (q.Adults < 1 || q.Children < 0 || minPrice < 0 || maxPrice < minPrice) throw new InvalidArgumentException("Invalid occupancy or price filter.");
        var available = await bookings.FindAvailableRoomsAsync(q, ct); var ids = available.Select(x => x.Id).ToArray();
        var rooms = await db.Phongs.Where(x => ids.Contains(x.Id) && (!roomTypeId.HasValue || x.LoaiPhongId == roomTypeId) && (view == null || x.LoaiView == view)).Select(x => x.Id).ToListAsync(ct);
        var result = new List<object>();
        foreach (var room in available.Where(x => rooms.Contains(x.Id)))
        {
            var quote = await bookings.PreviewPriceAsync(room.Id, q.CheckInDate, q.CheckOutDate, ct);
            if ((!minPrice.HasValue || quote.Total >= minPrice) && (!maxPrice.HasValue || quote.Total <= maxPrice)) result.Add(new { Room = room, Quote = quote });
        }
        return Ok(result);
    }
    [AllowAnonymous, HttpGet("public/rooms/{id:long}")]
    public async Task<IActionResult> PublicRoom(long id, CancellationToken ct)
    {
        var x = await db.Phongs.AsNoTracking().Include(x => x.LoaiPhong).SingleOrDefaultAsync(x => x.Id == id && x.TrangThai != "NGUNG_SU_DUNG" && x.LoaiPhong.TrangThai == "HOAT_DONG", ct) ?? throw new ResourceNotFoundException("Room was not found.");
        var amenities = await (from link in db.Set<LoaiPhongTienNghi>() join a in db.Set<TienNghi>() on link.TienNghiId equals a.Id where link.LoaiPhongId == x.LoaiPhongId && a.TrangThai == "HOAT_DONG" select a).ToListAsync(ct);
        return Ok(new { x.Id, x.SoPhong, x.Tang, x.HuongPhong, x.LoaiView, x.ViTri, x.MoTa, x.PhuThuDemMacDinh,
            RoomType = new { x.LoaiPhong.Id, x.LoaiPhong.TenLoaiPhong, x.LoaiPhong.SoNguoiLonToiDa, x.LoaiPhong.SoTreEmToiDa, x.LoaiPhong.DienTichM2, x.LoaiPhong.GiaCoBanDem }, Amenities = amenities,
            Images = await db.Set<HinhAnhPhong>().Where(i => i.PhongId == id).OrderBy(i => i.ThuTu).ToListAsync(ct),
            TypeImages = await db.Set<HinhAnhLoaiPhong>().Where(i => i.LoaiPhongId == x.LoaiPhongId).OrderBy(i => i.ThuTu).ToListAsync(ct) });
    }
    [AllowAnonymous, HttpGet("public/rooms/{id:long}/price")]
    public async Task<IActionResult> Price(long id, DateOnly checkInDate, DateOnly checkOutDate, CancellationToken ct) => Ok(await bookings.PreviewPriceAsync(id, checkInDate, checkOutDate, ct));
    [AllowAnonymous, HttpGet("public/room-types")]
    public async Task<IActionResult> Types(CancellationToken ct) => Ok(await db.LoaiPhongs.AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").Select(x => new { x.Id, x.TenLoaiPhong, x.SoNguoiLonToiDa, x.SoTreEmToiDa, x.DienTichM2, x.GiaCoBanDem, x.MoTa }).ToListAsync(ct));
    [HttpPut("rooms/{id:long}"), RequirePermission("ROOM_WRITE")]
    public async Task<IActionResult> Room(long id, RoomUpdateRequest r, CancellationToken ct)
    {
        await rooms.UpdateRoomAsync(id, new UpdateRoomRequest(r.Number, r.RoomTypeId, r.Floor ?? "", r.Direction ?? "",
            r.View ?? "", r.Location ?? "", r.Surcharge, r.Description ?? "", r.Note ?? ""), ct);
        return Ok(new { Id = id });
    }
    [HttpPut("room-types/{id:long}"), RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<IActionResult> Type(long id, RoomTypeUpdateRequest r, CancellationToken ct)
    {
        await rooms.UpdateRoomTypeAsync(id, new UpdateRoomTypeRequest(r.Name, r.MaxAdults, r.MaxChildren,
            r.Area, r.BasePrice, r.Description ?? "", r.Status), ct, replaceArea: true);
        return Ok(new { Id = id });
    }
    [HttpPatch("rooms/{id:long}/status"), RequirePermission("ROOM_WRITE")]
    public async Task<IActionResult> RoomStatus(long id, StatusRequest r, CancellationToken ct)
    {
        await rooms.SetStatusAsync(id, new SetRoomStatusRequest(r.Status), ct);
        return NoContent();
    }
    [HttpGet("rooms/calendar"), RequirePermission("ROOM_READ")]
    public async Task<IActionResult> Calendar(DateOnly from, DateOnly to, long? roomId, CancellationToken ct)
    {
        Input.Range(from, to); return Ok(await db.KhoaLichPhongs.AsNoTracking().Where(x => x.NgayLuuTru >= from && x.NgayLuuTru <= to && (!roomId.HasValue || x.PhongId == roomId) && (x.TrangThai != "HOLD" || x.HetHanGiuCho > DateTime.UtcNow)).OrderBy(x => x.NgayLuuTru).ThenBy(x => x.PhongId).ToListAsync(ct));
    }
    [HttpGet("rooms/map"), RequirePermission("ROOM_READ")]
    public async Task<IActionResult> Map(CancellationToken ct) => Ok(await db.Phongs.AsNoTracking().OrderBy(x => x.Tang).ThenBy(x => x.SoPhong).Select(x => new { x.Id, x.Tang, x.SoPhong, x.TrangThai, x.LoaiPhongId }).ToListAsync(ct));
    [HttpGet("amenities"), AllowAnonymous]
    public async Task<IActionResult> Amenities(CancellationToken ct) => Ok(await db.Set<TienNghi>().AsNoTracking().Where(x => x.TrangThai == "HOAT_DONG").ToListAsync(ct));
    [HttpPost("amenities"), RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<IActionResult> Amenity(AmenityRequest r, CancellationToken ct) { var x = new TienNghi { MaTienNghi = r.Code, TenTienNghi = r.Name, Icon = r.Icon, MoTa = r.Description }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/amenities/{x.Id}", x); }
    [HttpPut("room-types/{id:long}/amenities"), RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<IActionResult> AssignAmenities(long id, IdsRequest r, CancellationToken ct)
    {
        if (!await db.LoaiPhongs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Room type was not found.");
        var ids = r.Ids.Distinct().ToArray(); if (await db.Set<TienNghi>().CountAsync(x => ids.Contains(x.Id), ct) != ids.Length) throw new InvalidArgumentException("Unknown amenity.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); db.RemoveRange(await db.Set<LoaiPhongTienNghi>().Where(x => x.LoaiPhongId == id).ToListAsync(ct)); await db.SaveChangesAsync(ct);
        db.AddRange(ids.Select(x => new LoaiPhongTienNghi { LoaiPhongId = id, TienNghiId = x })); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent();
    }
    [HttpPost("rooms/{id:long}/images"), RequirePermission("ROOM_WRITE")]
    public async Task<IActionResult> RoomImage(long id, ImageRequest r, CancellationToken ct)
    {
        if (!await db.Phongs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Room was not found.");
        var x = new HinhAnhPhong { PhongId = id, Url = r.Url, AltText = r.AltText, LaAnhDaiDien = r.Cover, ThuTu = r.Order }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/rooms/{id}/images/{x.Id}", x);
    }
    [HttpPost("room-types/{id:long}/images"), RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<IActionResult> TypeImage(long id, ImageRequest r, CancellationToken ct)
    {
        if (!await db.LoaiPhongs.AnyAsync(x => x.Id == id, ct)) throw new ResourceNotFoundException("Room type was not found.");
        var x = new HinhAnhLoaiPhong { LoaiPhongId = id, Url = r.Url, AltText = r.AltText, LaAnhDaiDien = r.Cover, ThuTu = r.Order }; db.Add(x); await db.SaveChangesAsync(ct); return Created($"/v1/room-types/{id}/images/{x.Id}", x);
    }
    [HttpDelete("rooms/{id:long}/images/{imageId:long}"), RequirePermission("ROOM_WRITE")]
    public async Task<IActionResult> DeleteRoomImage(long id, long imageId, CancellationToken ct) { var x = await db.Set<HinhAnhPhong>().SingleOrDefaultAsync(x => x.Id == imageId && x.PhongId == id, ct) ?? throw new ResourceNotFoundException("Image was not found."); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
    [HttpDelete("room-types/{id:long}/images/{imageId:long}"), RequirePermission("ROOM_TYPE_WRITE")]
    public async Task<IActionResult> DeleteTypeImage(long id, long imageId, CancellationToken ct) { var x = await db.Set<HinhAnhLoaiPhong>().SingleOrDefaultAsync(x => x.Id == imageId && x.LoaiPhongId == id, ct) ?? throw new ResourceNotFoundException("Image was not found."); db.Remove(x); await db.SaveChangesAsync(ct); return NoContent(); }
}
