using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Application.Accounts;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tra cứu danh sách vai trò và thông tin chi tiết theo quyền ROLE_READ.</summary>
[Route("v1/roles")]
public sealed class RolesController(HotelDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("ROLE_READ")]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> List(CancellationToken cancellationToken)
    {
        var roles = await dbContext.VaiTros.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new RoleResponse(
                $"roles/{x.Id}", x.Id, x.MaVaiTro, x.TenVaiTro, x.MoTa, x.TrangThai))
            .ToListAsync(cancellationToken);
        return OkResponse<IReadOnlyList<RoleResponse>>(roles);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("ROLE_READ")]
    public async Task<ActionResult<RoleResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var role = await dbContext.VaiTros.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new Application.Common.ResourceNotFoundException($"Role roles/{id} was not found.");
        return OkResponse(new RoleResponse(
            $"roles/{role.Id}", role.Id, role.MaVaiTro, role.TenVaiTro, role.MoTa, role.TrangThai));
    }
}
