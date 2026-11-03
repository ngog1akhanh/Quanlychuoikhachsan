using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Services;

public sealed record ServiceRequest([param: Required, StringLength(30)] string Code, [param: Required, StringLength(120)] string Name,
    [param: StringLength(80)] string? Group, [param: Required, StringLength(30)] string Unit, decimal Price,
    [param: StringLength(1000)] string? Description, string Status = "HOAT_DONG");
public sealed record UseServiceRequest(long BookingId, long? RoomId, long ServiceId, decimal Quantity, [param: StringLength(500)] string? Note);
public sealed record ServicePaymentRequest(long PaymentMethodId, decimal Amount, [param: Required, StringLength(150)] string Reference, long? ShiftId = null);

public sealed class ServiceOperations(HotelDbContext db, CurrentActor actor)
{
    public async Task<SuDungDichVu> RequestAsync(UseServiceRequest r, CancellationToken ct)
    {
        var booking = await actor.BookingAsync(r.BookingId, "SERVICE_WRITE", ct);
        if (booking.TrangThai != "DANG_O") throw new InvalidArgumentException("Services are available during an active stay.");
        if (r.RoomId.HasValue && !await db.ChiTietDatPhongs.AnyAsync(x => x.DatPhongId == booking.Id && x.PhongId == r.RoomId && x.TrangThai == "DANG_O", ct))
            throw new InvalidArgumentException("Room must belong to the active stay.");
        Input.Quantity(r.Quantity);
        if (decimal.Round(r.Quantity, 2) != r.Quantity || r.Quantity > 9999999999.99m) throw new InvalidArgumentException("Service quantity supports at most two decimal places.");
        var service = await db.Set<DichVu>().SingleOrDefaultAsync(x => x.Id == r.ServiceId && x.TrangThai == "HOAT_DONG", ct) ?? throw new ResourceNotFoundException("Active service was not found.");
        var total = Input.Round(r.Quantity * service.DonGia); Input.Money(total, "total");
        var row = new SuDungDichVu { MaSuDung = $"DV{Guid.NewGuid():N}", DatPhongId = booking.Id, PhongId = r.RoomId, DichVuId = service.Id,
            TaiKhoanYeuCauId = actor.AccountId, ThoiGianYeuCau = DateTime.UtcNow, SoLuong = r.Quantity, DonGia = service.DonGia, ThanhTien = total, GhiChu = r.Note, TrangThai = "CHO_XU_LY" };
        db.Add(row); await db.SaveChangesAsync(ct); return row;
    }

    public async Task<object> TransitionAsync(long id, string status, CancellationToken ct)
    {
        Input.OneOf(status, "DANG_XU_LY", "HOAN_THANH", "DA_HUY");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.Set<SuDungDichVu>().FromSqlInterpolated($"SELECT * FROM su_dung_dich_vu WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var row = await db.Set<SuDungDichVu>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Service request was not found.");
        if (!((row.TrangThai == "CHO_XU_LY" && status is "DANG_XU_LY" or "DA_HUY") || (row.TrangThai == "DANG_XU_LY" && status == "HOAN_THANH")))
            throw new InvalidArgumentException("Invalid service state transition.");
        row.TrangThai = status;
        HoaDonDichVu? invoice = null;
        if (status == "HOAN_THANH")
        {
            row.ThoiGianPhucVu = DateTime.UtcNow;
            invoice = new HoaDonDichVu { MaHoaDon = $"HDV{Guid.NewGuid():N}", DatPhongId = row.DatPhongId, NhanVienLapId = await actor.EmployeeIdAsync(ct), NgayLap = DateTime.UtcNow,
                TongTien = row.ThanhTien, TrangThai = row.ThanhTien == 0 ? "DA_THANH_TOAN" : "DA_PHAT_HANH" };
            db.Add(invoice); await db.SaveChangesAsync(ct);
            db.Add(new ChiTietHoaDonDichVu { HoaDonDichVuId = invoice.Id, SuDungDichVuId = row.Id, ThanhTien = row.ThanhTien });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new { Request = row, Invoice = invoice };
    }

    public async Task<HoaDonDichVu> VoidAsync(long id, string reason, CancellationToken ct)
    {
        Input.Text(reason, 500, "reason");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.Set<HoaDonDichVu>().FromSqlInterpolated($"SELECT * FROM hoa_don_dich_vu WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var invoice = await db.Set<HoaDonDichVu>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Service invoice was not found.");
        if (invoice.TrangThai == "DA_HUY") throw new InvalidArgumentException("Invoice is already voided.");
        invoice.TrangThai = "DA_HUY";
        db.Add(new NhatKyHeThong { TaiKhoanId = actor.AccountId, HanhDong = "VOID", DoiTuong = "hoa_don_dich_vu", DoiTuongId = id.ToString(), DuLieuMoi = System.Text.Json.JsonSerializer.Serialize(new { reason }), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return invoice;
    }

    public async Task<ThanhToanDichVu> PayAsync(long id, ServicePaymentRequest r, CancellationToken ct)
    {
        Input.Money(r.Amount, "amount"); Input.Text(r.Reference, 150, "reference");
        if (r.Amount <= 0) throw new InvalidArgumentException("Amount must be positive.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsRelational()) await db.Set<HoaDonDichVu>().FromSqlInterpolated($"SELECT * FROM hoa_don_dich_vu WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var invoice = await db.Set<HoaDonDichVu>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Service invoice was not found.");
        var existing = await db.Set<ThanhToanDichVu>().SingleOrDefaultAsync(x => x.HoaDonDichVuId == id && x.MaGiaoDich == r.Reference, ct);
        if (existing is not null)
        {
            if (existing.SoTien != r.Amount || existing.PhuongThucId != r.PaymentMethodId) throw new AlreadyExistsException("Payment reference was used with different data.");
            return existing;
        }
        if (invoice.TrangThai is not ("DA_PHAT_HANH" or "TT_MOT_PHAN")) throw new InvalidArgumentException("Invoice cannot accept payments.");
        var method = await db.PhuongThucThanhToans.SingleOrDefaultAsync(x => x.Id == r.PaymentMethodId && x.TrangThai == "HOAT_DONG", ct) ?? throw new ResourceNotFoundException("Payment method was not found.");
        Input.OneOf(method.MaPhuongThuc, "TIEN_MAT", "CHUYEN_KHOAN", "THE", "CASH", "BANK_TRANSFER", "CARD");
        var employee = await actor.EmployeeIdAsync(ct);
        if (r.ShiftId.HasValue && !await db.CaLamViecs.AnyAsync(x => x.Id == r.ShiftId && x.NhanVienId == employee && x.TrangThai == "DANG_MO", ct)) throw new InvalidArgumentException("Shift must be open and belong to the cashier.");
        var paid = await db.Set<ThanhToanDichVu>().Where(x => x.HoaDonDichVuId == id && x.TrangThai == "THANH_CONG").SumAsync(x => (decimal?)x.SoTien, ct) ?? 0;
        if (paid + r.Amount > invoice.TongTien) throw new InvalidArgumentException("Payment exceeds remaining balance.");
        var row = new ThanhToanDichVu { MaThanhToan = $"TDV{Guid.NewGuid():N}", HoaDonDichVuId = id, NhanVienThuId = employee, PhuongThucId = r.PaymentMethodId,
            CaLamViecId = r.ShiftId, SoTien = r.Amount, ThoiGianThanhToan = DateTime.UtcNow, MaGiaoDich = r.Reference, TrangThai = "THANH_CONG" };
        db.Add(row); invoice.TrangThai = paid + r.Amount == invoice.TongTien ? "DA_THANH_TOAN" : "TT_MOT_PHAN";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row;
    }
}
