using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Rooms;

/// <summary>
/// Quản lý phòng Giai đoạn 1: sơ đồ phòng theo tầng kèm khách đang ở/khách sắp đến, sửa phòng,
/// đổi trạng thái vận hành, sửa loại phòng, lịch phòng theo ngày.
/// </summary>
public sealed class RoomAdminService(HotelDbContext db) : IRoomAdminService
{
    // Trạng thái nhân viên được đổi tay. DANG_O chỉ do nhận/trả phòng điều khiển.
    private static readonly string[] ManualStatuses = ["SAN_SANG", "CAN_DON", "DANG_DON", "BAO_TRI", "NGUNG_SU_DUNG"];

    public async Task<IReadOnlyList<RoomBoardItem>> GetBoardAsync(CancellationToken ct)
    {
        var rooms = await db.Phongs.AsNoTracking().Include(x => x.LoaiPhong).ToListAsync(ct);

        // Khách đang ở theo phòng.
        var inHouse = await db.ChiTietDatPhongs.AsNoTracking()
            .Where(d => d.DatPhong.TrangThai == "DANG_O")
            .Select(d => new { d.PhongId, d.DatPhongId, d.DatPhong.MaDatPhong, Guest = d.DatPhong.KhachHang.HoTen, d.DatPhong.DuKienTra })
            .ToListAsync(ct);

        // Khách sẽ đến hôm nay theo phòng.
        var start = HotelTime.ToUtc(HotelTime.Today.ToDateTime(TimeOnly.MinValue));
        var end = start.AddDays(1);
        var arrivals = await db.ChiTietDatPhongs.AsNoTracking()
            .Where(d => (d.DatPhong.TrangThai == "BOOKED" || d.DatPhong.TrangThai == "HOLD") && d.DatPhong.DuKienNhan >= start && d.DatPhong.DuKienNhan < end)
            .Select(d => new { d.PhongId, d.DatPhongId, d.DatPhong.MaDatPhong, Guest = d.DatPhong.KhachHang.HoTen, d.DatPhong.DuKienNhan })
            .ToListAsync(ct);

        return rooms
            .OrderBy(r => FloorOrder(r.Tang)).ThenBy(r => r.SoPhong)
            .Select(r =>
            {
                var stay = inHouse.FirstOrDefault(x => x.PhongId == r.Id);
                var arrival = arrivals.FirstOrDefault(x => x.PhongId == r.Id);
                return ToItem(r, stay?.DatPhongId, stay?.MaDatPhong, stay?.Guest, HotelTime.Local(stay?.DuKienTra),
                    arrival?.DatPhongId, arrival?.MaDatPhong, arrival?.Guest, HotelTime.Local(arrival?.DuKienNhan));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RoomTypeSummary>> GetRoomTypesAsync(CancellationToken ct)
        => await db.LoaiPhongs.AsNoTracking().OrderBy(x => x.GiaCoBanDem)
            .Select(x => new RoomTypeSummary(x.Id, x.MaLoaiPhong, x.TenLoaiPhong, x.SoNguoiLonToiDa, x.SoTreEmToiDa, x.DienTichM2,
                x.GiaCoBanDem, x.MoTa, x.TrangThai,
                x.Phongs.Count(p => p.TrangThai != "NGUNG_SU_DUNG"),
                x.Phongs.Count(p => p.TrangThai == "SAN_SANG"),
                x.Phongs.Count(p => p.TrangThai == "DANG_O")))
            .ToListAsync(ct);

    public async Task<RoomBoardItem> UpdateRoomAsync(long roomId, UpdateRoomRequest request, CancellationToken ct)
    {
        var room = await db.Phongs.Include(x => x.LoaiPhong).SingleOrDefaultAsync(x => x.Id == roomId, ct)
            ?? throw new ResourceNotFoundException($"Không tìm thấy phòng #{roomId}.");

        if (!string.IsNullOrWhiteSpace(request.RoomNumber) && request.RoomNumber.Trim() != room.SoPhong)
        {
            var number = Input.Text(request.RoomNumber, 20, "room_number");
            if (await db.Phongs.AnyAsync(x => x.SoPhong == number && x.Id != roomId, ct))
                throw new AlreadyExistsException($"Số phòng {number} đã tồn tại.");
            room.SoPhong = number;
        }
        if (request.RoomTypeId is { } typeId && typeId != room.LoaiPhongId)
        {
            var type = await db.LoaiPhongs.SingleOrDefaultAsync(x => x.Id == typeId && x.TrangThai == "HOAT_DONG", ct)
                ?? throw new ResourceNotFoundException("Không tìm thấy loại phòng.");
            room.LoaiPhongId = type.Id;
            room.LoaiPhong = type;
        }
        if (request.NightSurcharge is { } surcharge)
        {
            Input.Money(surcharge, "night_surcharge");
            if (surcharge < 0) throw new InvalidArgumentException("Phụ thu không được âm.");
            room.PhuThuDemMacDinh = surcharge;
        }
        Input.Optional(request.Floor, 20, "floor");
        Input.Optional(request.Direction, 80, "direction");
        Input.Optional(request.View, 100, "view");
        Input.Optional(request.Position, 150, "position");
        Input.Optional(request.Description, 1000, "description");
        Input.Optional(request.Note, 500, "note");
        if (request.Floor is not null) room.Tang = Clean(request.Floor);
        if (request.Direction is not null) room.HuongPhong = Clean(request.Direction);
        if (request.View is not null) room.LoaiView = Clean(request.View);
        if (request.Position is not null) room.ViTri = Clean(request.Position);
        if (request.Description is not null) room.MoTa = Clean(request.Description);
        if (request.Note is not null) room.GhiChu = Clean(request.Note);

        await db.SaveChangesAsync(ct);
        return (await GetBoardAsync(ct)).Single(x => x.Id == roomId);
    }

    public async Task<RoomBoardItem> SetStatusAsync(long roomId, SetRoomStatusRequest request, CancellationToken ct)
    {
        // Khóa giao dịch để trạng thái phòng không đè lên thao tác nhận phòng.
        await using var tx = await BookingTransaction.BeginAsync(db, ct);
        var status = (request.Status ?? "").Trim().ToUpperInvariant();
        if (!ManualStatuses.Contains(status))
            throw new InvalidArgumentException("Trạng thái không hợp lệ. \"Đang ở\" chỉ được đặt khi nhận phòng.");
        var room = await db.Phongs.SingleOrDefaultAsync(x => x.Id == roomId, ct)
            ?? throw new ResourceNotFoundException($"Không tìm thấy phòng #{roomId}.");
        if (room.TrangThai == "DANG_O" || await db.ChiTietDatPhongs.AnyAsync(x => x.PhongId == roomId && x.TrangThai == "DANG_O", ct))
            throw new InvalidArgumentException("Phòng đang có khách. Hãy làm thủ tục trả phòng hoặc đổi phòng trước.");

        if (status is "NGUNG_SU_DUNG" or "BAO_TRI")
        {
            var today = HotelTime.Today;
            var hasFuture = await db.KhoaLichPhongs.AnyAsync(x => x.PhongId == roomId && x.NgayLuuTru >= today && (x.TrangThai != "HOLD" || x.HetHanGiuCho > DateTime.UtcNow), ct);
            if (hasFuture) throw new InvalidArgumentException("Phòng còn booking trong tương lai. Hãy đổi phòng cho các booking đó trước.");
        }

        Input.Optional(request.Note, 500, "note");
        room.TrangThai = status;
        if (!string.IsNullOrWhiteSpace(request.Note)) room.GhiChu = request.Note.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await GetBoardAsync(ct)).Single(x => x.Id == roomId);
    }

    public async Task<RoomTypeSummary> UpdateRoomTypeAsync(long roomTypeId, UpdateRoomTypeRequest request, CancellationToken ct, bool replaceArea = false)
    {
        var type = await db.LoaiPhongs.SingleOrDefaultAsync(x => x.Id == roomTypeId, ct)
            ?? throw new ResourceNotFoundException($"Không tìm thấy loại phòng #{roomTypeId}.");
        if (!string.IsNullOrWhiteSpace(request.Name)) type.TenLoaiPhong = Input.Text(request.Name, 100, "name");
        if (request.MaxAdults is { } adults)
        {
            if (adults < 1) throw new InvalidArgumentException("Sức chứa người lớn phải ≥ 1.");
            type.SoNguoiLonToiDa = adults;
        }
        if (request.MaxChildren is { } children)
        {
            if (children < 0) throw new InvalidArgumentException("Sức chứa trẻ em không được âm.");
            type.SoTreEmToiDa = children;
        }
        if (replaceArea) type.DienTichM2 = request.AreaM2;
        if (request.AreaM2 is { } area)
        {
            if (area <= 0 || area > 999999.99m) throw new InvalidArgumentException("Diện tích phải lớn hơn 0.");
            type.DienTichM2 = area;
        }
        if (request.BaseNightlyPrice is { } price)
        {
            // Đổi giá cơ bản không ảnh hưởng booking cũ vì giá từng đêm đã chốt trong gia_dem_dat_phong (mục 10.2).
            Input.Money(price, "base_nightly_price");
            if (price < 0) throw new InvalidArgumentException("Giá không được âm.");
            type.GiaCoBanDem = price;
        }
        Input.Optional(request.Description, 10000, "description");
        if (request.Description is not null) type.MoTa = Clean(request.Description);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            if (status is not ("HOAT_DONG" or "NGUNG_SU_DUNG")) throw new InvalidArgumentException("Trạng thái loại phòng: HOAT_DONG hoặc NGUNG_SU_DUNG.");
            type.TrangThai = status;
        }
        await db.SaveChangesAsync(ct);
        return (await GetRoomTypesAsync(ct)).Single(x => x.Id == roomTypeId);
    }

    public async Task<RoomCalendar> GetCalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        if (toDate < fromDate) throw new InvalidArgumentException("Ngày kết thúc phải sau ngày bắt đầu.");
        if (toDate.DayNumber - fromDate.DayNumber > 62) throw new InvalidArgumentException("Lịch phòng xem tối đa 62 ngày.");
        var now = DateTime.UtcNow;

        var rooms = (await db.Phongs.AsNoTracking().Include(x => x.LoaiPhong).ToListAsync(ct))
            .OrderBy(r => FloorOrder(r.Tang)).ThenBy(r => r.SoPhong)
            .Select(r => new RoomCalendarRoom(r.Id, r.SoPhong, r.Tang, r.LoaiPhong.MaLoaiPhong, r.TrangThai))
            .ToList();

        var cells = await (from k in db.KhoaLichPhongs.AsNoTracking()
                           join d in db.ChiTietDatPhongs on k.ChiTietDatPhongId equals d.Id
                           join b in db.DatPhongs on d.DatPhongId equals b.Id
                           where k.NgayLuuTru >= fromDate && k.NgayLuuTru <= toDate
                                 && (k.TrangThai != "HOLD" || k.HetHanGiuCho > now)
                           orderby k.PhongId, k.NgayLuuTru
                           select new RoomCalendarCell(k.PhongId, k.NgayLuuTru, k.TrangThai, b.Id, b.MaDatPhong, b.TrangThai, b.KhachHang.HoTen))
            .ToListAsync(ct);

        return new RoomCalendar(fromDate, toDate, rooms, cells);
    }

    private static RoomBoardItem ToItem(Phong r, long? stayId, string? stayCode, string? stayGuest, DateTime? stayOut,
        long? arrivalId, string? arrivalCode, string? arrivalGuest, DateTime? arrivalTime)
        => new(r.Id, r.MaPhong, r.SoPhong, r.Tang, r.HuongPhong, r.LoaiView, r.ViTri, r.LoaiPhongId, r.LoaiPhong.MaLoaiPhong,
            r.LoaiPhong.TenLoaiPhong, r.LoaiPhong.GiaCoBanDem, r.PhuThuDemMacDinh, r.TrangThai, r.MoTa, r.GhiChu,
            stayId, stayCode, stayGuest, stayOut, arrivalId, arrivalCode, arrivalGuest, arrivalTime);

    // Tầng lưu dạng chuỗi ("1", "2", "Trệt"...): tầng là số thì sắp theo số, còn lại xếp cuối.
    private static int FloorOrder(string? floor) => int.TryParse(floor, out var n) ? n : int.MaxValue;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
