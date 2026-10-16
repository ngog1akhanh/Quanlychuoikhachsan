using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Accounts;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Cung cấp đăng ký khách hàng và đăng nhập để nhận JWT.</summary>
[Route("v1/auth")]
public sealed class AuthController(IAccountService accountService) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<CustomerResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Register(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await accountService.RegisterCustomerAsync(request, cancellationToken);
        return Created($"/v1/{customer.Name}", customer);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await accountService.LoginAsync(request, cancellationToken));
}
