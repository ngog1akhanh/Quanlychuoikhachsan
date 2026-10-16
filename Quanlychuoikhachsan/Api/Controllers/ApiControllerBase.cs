using Microsoft.AspNetCore.Mvc;
using Quanlychuoikhachsan.Api.Contracts;

namespace Quanlychuoikhachsan.Api.Controllers;

/// <summary>Dùng chung request ID và các helper phản hồi cho controller API.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string? RequestId => HttpContext.TraceIdentifier;

    protected ActionResult<ApiResponse<T>> Success<T>(T data)
        => Ok(new ApiResponse<T>(data, RequestId));

    protected ActionResult<ApiResponse<T>> Created<T>(string uri, T data)
        => base.Created(uri, new ApiResponse<T>(data, RequestId));

    protected ActionResult<ApiAcceptedResponse> AcceptedOperation(string operation)
        => Accepted(new ApiAcceptedResponse(operation, RequestId));

    protected ActionResult<T> OkResponse<T>(T value) => Ok(value);

    protected ActionResult<T> CreatedResponse<T>(string routeName, object routeValues, T value)
        => CreatedAtRoute(routeName, routeValues, value);

    protected IActionResult NoContentResponse() => NoContent();
}
