namespace Quanlychuoikhachsan.Application.Common;

public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string status, string message, IReadOnlyList<object>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Status = status;
        Details = details ?? [];
    }

    public int StatusCode { get; }
    public string Status { get; }
    public IReadOnlyList<object> Details { get; }
}

public sealed class InvalidArgumentException(string message, IReadOnlyList<object>? details = null)
    : ApiException(StatusCodes.Status400BadRequest, "INVALID_ARGUMENT", message, details);

public sealed class UnauthenticatedException(string message = "Authentication is required.")
    : ApiException(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", message);

public sealed class PermissionDeniedException(string message = "You do not have permission to perform this operation.")
    : ApiException(StatusCodes.Status403Forbidden, "PERMISSION_DENIED", message);

public sealed class ResourceNotFoundException(string message)
    : ApiException(StatusCodes.Status404NotFound, "NOT_FOUND", message);

public sealed class AlreadyExistsException(string message)
    : ApiException(StatusCodes.Status409Conflict, "ALREADY_EXISTS", message);

public sealed class ServiceUnavailableException(string message)
    : ApiException(StatusCodes.Status503ServiceUnavailable, "UNAVAILABLE", message);
