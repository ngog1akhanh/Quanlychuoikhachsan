using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed partial class BookingService
{
    // Khôi phục đúng BOOKED/HOLD/HET_HAN khi từ chối hủy.
    public async Task<YeuCauHuyDatPhong> RejectCancellationAsync(long id, string reason, CancellationToken ct)
    {
        Input.Text(reason, 1000, "reason");
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        var x = await dbContext.YeuCauHuyDatPhongs.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Cancellation request was not found.");
        if (x.TrangThai != "CHO_DUYET") throw new InvalidArgumentException("Request is already resolved.");
        var b = await dbContext.DatPhongs.SingleAsync(b => b.Id == x.DatPhongId, ct);
        var hasDeposit = await dbContext.PhieuCocs.AnyAsync(p => p.DatPhongId == b.Id && p.TrangThai == "DA_THANH_TOAN", ct);
        var details = await dbContext.ChiTietDatPhongs.Where(d => d.DatPhongId == b.Id).ToListAsync(ct);
        var detailIds = details.Select(d => d.Id).ToArray();
        var holds = await dbContext.KhoaLichPhongs.Where(h => detailIds.Contains(h.ChiTietDatPhongId)).ToListAsync(ct);
        var expired = !hasDeposit && (holds.Count == 0 || holds.Any(h => h.HetHanGiuCho <= DateTime.UtcNow));
        b.TrangThai = hasDeposit ? "BOOKED" : expired ? "HET_HAN" : "HOLD"; b.UpdatedAt = DateTime.UtcNow;
        foreach (var detail in details) detail.TrangThai = b.TrangThai;
        if (expired) dbContext.KhoaLichPhongs.RemoveRange(holds);
        x.TrangThai = "TU_CHOI"; x.GhiChuXuLy = reason; x.NhanVienXuLyId = await actor.EmployeeIdAsync(ct); x.ThoiGianXuLy = DateTime.UtcNow;
        await notifications.BookingAsync(b, actor.AccountId, "CHO_DUYET_HUY", reason, ct); await dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct); return x;
    }
}
