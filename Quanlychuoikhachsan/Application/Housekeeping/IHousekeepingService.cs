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

/// <summary>T?o công vi?c d?n phòng và l?u các ghi nh?n tình tr?ng phòng.</summary>
public sealed class HousekeepingService(HotelDbContext db) : IHousekeepingService
{
    public async Task<HousekeepingTaskResponse> CreateCheckoutTaskAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (!await db.Phongs.AnyAsync(x => x.Id == request.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        var task = new CongViecBuongPhong { MaCongViec = $"HK{Guid.NewGuid():N}"[..40], PhongId = request.RoomId, DatPhongId = request.BookingId, LoaiCongViec = "DON_CHECKOUT", NhanVienPhanCongId = request.EmployeeId, MucDoUuTien = "CAO", TrangThai = "CHO_XU_LY" };
        db.CongViecBuongPhongs.Add(task); await db.SaveChangesAsync(ct); return ToResponse(task);
    }

    public async Task<IReadOnlyList<HousekeepingTaskResponse>> ListTasksAsync(CancellationToken ct) => await db.CongViecBuongPhongs.AsNoTracking().OrderBy(x => x.TrangThai).ThenBy(x => x.Id).Select(x => new HousekeepingTaskResponse(x.Id, $"housekeeping-tasks/{x.Id}", x.PhongId, x.DatPhongId, x.LoaiCongViec, x.TrangThai)).ToListAsync(ct);

    public async Task<RoomConditionResponse> RecordConditionAsync(CreateRoomConditionRequest request, CancellationToken ct)
    {
        if (request.EstimatedCost < 0 || request.ActualCost < 0) throw new InvalidArgumentException("Compensation costs cannot be negative.");
        if (!await db.Phongs.AnyAsync(x => x.Id == request.RoomId, ct)) throw new ResourceNotFoundException("Room was not found.");
        var condition = new TinhTrangPhong { PhongId = request.RoomId, DatPhongId = request.BookingId, TaiSanCaTheId = request.AssetId, LoaiTinhTrang = request.Type, MucDo = request.Severity, MoTa = request.Description, AnhMinhChung = request.Evidence, ChiPhiDuKien = request.EstimatedCost, ChiPhiThucTe = request.ActualCost, DoKhachGayRa = request.CausedByGuest, GhiNhanLuc = DateTime.UtcNow };
        db.TinhTrangPhongs.Add(condition); await db.SaveChangesAsync(ct);
        return new(condition.Id, condition.PhongId, condition.LoaiTinhTrang, condition.TrangThaiXuLy, condition.ChiPhiThucTe, condition.DoKhachGayRa);
    }

    private static HousekeepingTaskResponse ToResponse(CongViecBuongPhong x) => new(x.Id, $"housekeeping-tasks/{x.Id}", x.PhongId, x.DatPhongId, x.LoaiCongViec, x.TrangThai);
}
