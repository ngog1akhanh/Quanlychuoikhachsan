using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Accounts;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tra cứu hồ sơ nhân viên và chức vụ theo quyền STAFF_READ.</summary>
[Route("v1/staff")]
public sealed class StaffController(HotelDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("STAFF_READ")]
    public async Task<ActionResult<IReadOnlyList<StaffResponse>>> List(CancellationToken cancellationToken)
    {
        var staff = (await dbContext.NhanViens.AsNoTracking().Include(x => x.ChucVu)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)).Select(ToResponse).ToArray();
        return OkResponse<IReadOnlyList<StaffResponse>>(staff);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("STAFF_READ")]
    public async Task<ActionResult<StaffResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var staff = await dbContext.NhanViens.AsNoTracking().Include(x => x.ChucVu)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new Application.Common.ResourceNotFoundException($"Staff staff/{id} was not found.");
        return OkResponse(ToResponse(staff));
    }

    private static StaffResponse ToResponse(Domain.Entities.NhanVien staff) => new(
        $"staff/{staff.Id}", staff.Id, staff.MaNhanVien, staff.HoTen, staff.ChucVuId,
        staff.ChucVu.MaChucVu, staff.ChucVu.TenChucVu, staff.Email, staff.SoDienThoai,
        staff.TrangThai, DateTime.SpecifyKind(staff.CreatedAt, DateTimeKind.Utc));
}
