using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Common;

/// <summary>
/// Gắn khoản thu vào ca thu ngân. Client gửi ca cụ thể thì kiểm tra ca đó thuộc nhân viên và đang mở;
/// không gửi thì tự lấy ca đang mở của chính nhân viên thu tiền. Chưa mở ca → null (vẫn thu được, nhưng không vào đối soát).
/// </summary>
public static class CashierShift
{
    public static async Task<long?> ResolveAsync(HotelDbContext db, long? shiftId, long? employeeId, CancellationToken ct)
    {
        if (employeeId is null) return null;
        if (shiftId.HasValue)
        {
            if (!await db.CaLamViecs.AnyAsync(x => x.Id == shiftId && x.NhanVienId == employeeId && x.TrangThai == "DANG_MO", ct))
                throw new InvalidArgumentException("Ca thu ngân không thuộc về bạn hoặc đã đóng.");
            return shiftId;
        }
        return await db.CaLamViecs.Where(x => x.NhanVienId == employeeId && x.TrangThai == "DANG_MO")
            .OrderByDescending(x => x.BatDauLuc).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
    }
}
