using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Housekeeping;

public interface IHousekeepingService
{
    Task<HousekeepingTaskResponse> CreateCheckoutTaskAsync(CheckoutRequest request, CancellationToken ct);
    Task<IReadOnlyList<HousekeepingTaskResponse>> ListTasksAsync(CancellationToken ct);
    Task<RoomConditionResponse> RecordConditionAsync(CreateRoomConditionRequest request, CancellationToken ct);
}
public sealed class HousekeepingService(HotelDbContext db, CurrentActor actor) : IHousekeepingService
{
    public async Task<HousekeepingTaskResponse> CreateCheckoutTaskAsync(CheckoutRequest request, CancellationToken ct)
    {
        var employee = await actor.EmployeeIdAsync(ct);
        if (!await db.ChiTietDatPhongs.AnyAsync(x => x.DatPhongId == request.BookingId && x.PhongId == request.RoomId && x.TrangThai == "DA_TRA", ct)) throw new InvalidArgumentException("A checked-out room is required.");
        var existing = await db.CongViecBuongPhongs.FirstOrDefaultAsync(x => x.DatPhongId == request.BookingId && x.PhongId == request.RoomId && x.LoaiCongViec == "DON_CHECKOUT", ct);
        if (existing is not null) return ToResponse(existing);
        if (!await db.Phongs.AnyAsync(x => x.Id == request.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        var task = new CongViecBuongPhong { MaCongViec = $"HK{Guid.NewGuid():N}", PhongId = request.RoomId, DatPhongId = request.BookingId, LoaiCongViec = "DON_CHECKOUT", NhanVienPhanCongId = request.EmployeeId, MucDoUuTien = "CAO", TrangThai = "CHO_XU_LY" };
        db.CongViecBuongPhongs.Add(task); await db.SaveChangesAsync(ct); return ToResponse(task);
    }

    public async Task<IReadOnlyList<HousekeepingTaskResponse>> ListTasksAsync(CancellationToken ct) => await db.CongViecBuongPhongs.AsNoTracking().OrderBy(x => x.TrangThai).ThenBy(x => x.Id).Select(x => new HousekeepingTaskResponse(x.Id, $"housekeeping-tasks/{x.Id}", x.PhongId, x.DatPhongId, x.LoaiCongViec, x.TrangThai)).ToListAsync(ct);

    public async Task<RoomConditionResponse> RecordConditionAsync(CreateRoomConditionRequest request, CancellationToken ct)
    {
        Input.OneOf(request.Severity, "NHE", "TRUNG_BINH", "NGHIEM_TRONG"); Input.Text(request.Description, 1500, "description"); Input.Text(request.Type, 40, "type");
        Input.Money(request.EstimatedCost, "estimated_cost"); Input.Money(request.ActualCost, "actual_cost");
        if (request.BookingId.HasValue && !await db.ChiTietDatPhongs.AnyAsync(x => x.DatPhongId == request.BookingId && x.PhongId == request.RoomId, ct)) throw new InvalidArgumentException("Room does not belong to the booking.");
        if (request.AssetId.HasValue && !await db.TaiSanCaThes.AnyAsync(x => x.Id == request.AssetId && x.PhongId == request.RoomId, ct)) throw new InvalidArgumentException("Asset does not belong to the room.");
        if (request.CausedByGuest && !request.BookingId.HasValue) throw new InvalidArgumentException("Guest-caused damage requires a booking.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (request.CausedByGuest && await db.HoaDonPhongs.AnyAsync(x => x.DatPhongId == request.BookingId && x.TrangThai != "NHAP" && x.TrangThai != "DA_HUY", ct)) throw new InvalidArgumentException("Charges are already invoiced; void and replace the invoice first.");
        if (request.EstimatedCost < 0 || request.ActualCost < 0) throw new InvalidArgumentException("Compensation costs cannot be negative.");
        if (!await db.Phongs.AnyAsync(x => x.Id == request.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        var condition = new TinhTrangPhong { PhongId = request.RoomId, DatPhongId = request.BookingId, TaiSanCaTheId = request.AssetId, LoaiTinhTrang = request.Type, MucDo = request.Severity, MoTa = request.Description, AnhMinhChung = request.Evidence, ChiPhiDuKien = request.EstimatedCost, ChiPhiThucTe = request.ActualCost, DoKhachGayRa = request.CausedByGuest, GhiNhanLuc = DateTime.UtcNow };
        condition.NhanVienGhiNhanId = await actor.EmployeeIdAsync(ct);
        if (request.Severity == "NGHIEM_TRONG") (await db.Phongs.SingleAsync(x => x.Id == request.RoomId, ct)).TrangThai = "BAO_TRI";
        db.TinhTrangPhongs.Add(condition); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(condition.Id, condition.PhongId, condition.LoaiTinhTrang, condition.TrangThaiXuLy, condition.ChiPhiThucTe, condition.DoKhachGayRa);
    }

    private static HousekeepingTaskResponse ToResponse(CongViecBuongPhong x) => new(x.Id, $"housekeeping-tasks/{x.Id}", x.PhongId, x.DatPhongId, x.LoaiCongViec, x.TrangThai);
}
