using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Domain.Entities;

namespace Quanlychuoikhachsan.Application.Bookings;

public sealed record BookingGuest(long DetailId, string FullName, DateOnly? BirthDate, string? Gender,
    string? DocumentType, string? DocumentNumber, string? Nationality, bool Representative);

public sealed partial class BookingService
{
    // Quy tắc khai báo lưu trú dùng chung cho thao tác thêm và thay danh sách.
    private static KhachLuuTru GuestEntity(BookingGuest guest)
    {
        if (guest.BirthDate > HotelTime.Today) throw new InvalidArgumentException("Birth date cannot be in the future.");
        Input.Optional(guest.Gender, 20, "gender");
        Input.Optional(guest.DocumentType, 30, "document_type");
        Input.Optional(guest.Nationality, 80, "nationality");
        return new KhachLuuTru
        {
            ChiTietDatPhongId = guest.DetailId, HoTen = Input.Text(guest.FullName, 120, "full_name"),
            NgaySinh = guest.BirthDate, GioiTinh = guest.Gender, LoaiGiayTo = guest.DocumentType,
            SoGiayTo = Input.Text(guest.DocumentNumber, 50, "document_number"),
            QuocTich = guest.Nationality, LaKhachDaiDien = guest.Representative
        };
    }

    public async Task<KhachLuuTru> AddGuestAsync(long bookingId, BookingGuest guest, CancellationToken ct)
    {
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var detail = await dbContext.ChiTietDatPhongs.SingleOrDefaultAsync(x => x.Id == guest.DetailId && x.DatPhongId == bookingId && (x.TrangThai == "BOOKED" || x.TrangThai == "DANG_O"), ct)
            ?? throw new ResourceNotFoundException("Active booked room was not found.");
        if (await dbContext.Set<KhachLuuTru>().CountAsync(x => x.ChiTietDatPhongId == detail.Id, ct) >= detail.SoNguoiLon + detail.SoTreEm)
            throw new InvalidArgumentException("Guest count exceeds this room's reservation.");
        if (guest.Representative && await dbContext.Set<KhachLuuTru>().AnyAsync(x => x.ChiTietDatPhongId == detail.Id && x.LaKhachDaiDien, ct))
            throw new InvalidArgumentException("This room already has a representative.");
        var entity = GuestEntity(guest);
        dbContext.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entity;
    }

    public async Task ReplaceGuestsAsync(long bookingId, IReadOnlyList<BookingGuest> guests, CancellationToken ct)
    {
        await using var tx = await BookingTransaction.BeginAsync(dbContext, ct);
        await LockBookingAsync(bookingId, ct);
        var details = await dbContext.ChiTietDatPhongs.Where(x => x.DatPhongId == bookingId && (x.TrangThai == "BOOKED" || x.TrangThai == "DANG_O")).ToListAsync(ct);
        if (details.Count == 0) throw new ResourceNotFoundException("Active booked rooms were not found.");
        foreach (var group in guests.GroupBy(x => x.DetailId))
        {
            var detail = details.SingleOrDefault(x => x.Id == group.Key) ?? throw new InvalidArgumentException("Guest room does not belong to this booking.");
            if (group.Count() > detail.SoNguoiLon + detail.SoTreEm || group.Count(x => x.Representative) > 1)
                throw new InvalidArgumentException("Invalid guest count or duplicate representative.");
        }
        var entities = guests.Select(GuestEntity).ToList();
        var ids = details.Select(x => x.Id).ToArray();
        dbContext.RemoveRange(await dbContext.Set<KhachLuuTru>().Where(x => ids.Contains(x.ChiTietDatPhongId)).ToListAsync(ct));
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
