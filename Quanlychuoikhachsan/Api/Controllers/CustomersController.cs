using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Accounts;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Tra cứu, phân trang và cập nhật hồ sơ khách hàng.</summary>
[Route("v1/customers")]
public sealed class CustomersController(HotelDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("CUSTOMER_READ")]
    [ProducesResponseType<CustomerPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerPageResponse>> List(
        [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "pageToken")] string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = DecodePageToken(pageToken);
        var query = dbContext.KhachHangs.AsNoTracking().OrderBy(x => x.Id);
        var total = await query.CountAsync(cancellationToken);
        var customers = await query.Skip(offset).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasNext = customers.Count > pageSize;
        if (hasNext) customers.RemoveAt(pageSize);

        var response = new CustomerPageResponse(
            customers.Select(AccountService.ToCustomerResponse).ToArray(),
            hasNext ? EncodePageToken(offset + pageSize) : null,
            total);
        return OkResponse(response);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("CUSTOMER_READ")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var customer = await dbContext.KhachHangs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Customer customers/{id} was not found.");
        return OkResponse(AccountService.ToCustomerResponse(customer));
    }

    [HttpPatch("{id:long}")]
    [RequirePermission("CUSTOMER_WRITE")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> Update(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.KhachHangs.Include(x => x.TaiKhoan)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException($"Customer customers/{id} was not found.");

        var email = Normalize(request.Email);
        var phone = Normalize(request.PhoneNumber);
        if (email is not null || phone is not null)
        {
            var duplicateCustomer = await dbContext.KhachHangs.AnyAsync(x => x.Id != id &&
                ((email != null && x.Email == email) || (phone != null && x.SoDienThoai == phone)), cancellationToken);
            var duplicateAccount = await dbContext.TaiKhoans.AnyAsync(x => x.Id != customer.TaiKhoanId &&
                ((email != null && x.Email == email) || (phone != null && x.SoDienThoai == phone)), cancellationToken);
            if (duplicateCustomer || duplicateAccount)
                throw new AlreadyExistsException("The email or phone_number is already registered.");
        }

        if (request.FullName is not null) customer.HoTen = request.FullName.Trim();
        if (request.Email is not null) customer.Email = email;
        if (request.PhoneNumber is not null) customer.SoDienThoai = phone;
        if (request.BirthDate.HasValue) customer.NgaySinh = request.BirthDate;
        if (request.Gender is not null) customer.GioiTinh = request.Gender;
        if (request.IdentityNumber is not null) customer.CccdHoChieu = request.IdentityNumber;
        if (request.Address is not null) customer.DiaChi = request.Address;
        if (request.Note is not null) customer.GhiChu = request.Note;
        customer.UpdatedAt = DateTime.UtcNow;

        if (customer.TaiKhoan is not null)
        {
            if (request.Email is not null) customer.TaiKhoan.Email = email;
            if (request.PhoneNumber is not null) customer.TaiKhoan.SoDienThoai = phone;
            customer.TaiKhoan.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OkResponse(AccountService.ToCustomerResponse(customer));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static int DecodePageToken(string? pageToken)
    {
        if (string.IsNullOrWhiteSpace(pageToken)) return 0;
        try
        {
            var bytes = Convert.FromBase64String(pageToken.Replace('-', '+').Replace('_', '/') + new string('=', (4 - pageToken.Length % 4) % 4));
            var payload = JsonSerializer.Deserialize<PageToken>(bytes) ?? throw new JsonException();
            return payload.Offset >= 0 ? payload.Offset : throw new JsonException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidArgumentException("page_token is invalid.");
        }
    }

    private static string EncodePageToken(int offset)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PageToken(offset));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record PageToken(int Offset);
}
