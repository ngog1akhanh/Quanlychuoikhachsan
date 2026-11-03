using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;

namespace Quanlychuoikhachsan.Infrastructure.Middleware;

/// <summary>Chuyển exception nghiệp vụ, database và lỗi không dự kiến thành JSON lỗi API.</summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (statusCode, status, message, details) = MapException(exception);

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "API request failed with {StatusCode} for {Method} {Path}",
                statusCode, context.Request.Method, context.Request.Path);
        }

        if (context.Response.HasStarted)
        {
            logger.LogWarning("Cannot write Google error response because the HTTP response has already started.");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        if (UsesPaymentEnvelope(context.Request.Path))
        {
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new PaymentApiResponse<object?>(false, message, null, context.TraceIdentifier),
                JsonOptions,
                context.RequestAborted);
            return;
        }

        var response = new GoogleErrorResponse(new GoogleError(
            statusCode,
            message,
            status,
            details.Select(detail => new GoogleErrorDetail(
                "type.googleapis.com/google.rpc.ErrorInfo",
                detail)).ToArray()));

        await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions, context.RequestAborted);
    }

    private static bool UsesPaymentEnvelope(PathString path)
        => path.StartsWithSegments("/v1/payments/vietqr") ||
           path.Value?.Contains("/vietqr-payment", StringComparison.OrdinalIgnoreCase) == true;

    private (int Code, string Status, string Message, IReadOnlyList<object> Details) MapException(Exception exception)
    {
        // Ánh xạ lỗi MySQL thường gặp sang mã HTTP ổn định cho client.
        if (exception is ApiException apiException)
        {
            return (apiException.StatusCode, apiException.Status, apiException.Message, apiException.Details);
        }

        if (exception is ValidationException validationException)
        {
            return (StatusCodes.Status400BadRequest, "INVALID_ARGUMENT", validationException.Message, []);
        }

        if (exception is ArgumentException or FormatException)
        {
            return (StatusCodes.Status400BadRequest, "INVALID_ARGUMENT", exception.Message, []);
        }

        if (exception is KeyNotFoundException)
        {
            return (StatusCodes.Status404NotFound, "NOT_FOUND", exception.Message, []);
        }

        if (exception is UnauthorizedAccessException)
        {
            return (StatusCodes.Status403Forbidden, "PERMISSION_DENIED", exception.Message, []);
        }

        if (exception is DbUpdateConcurrencyException)
            return (StatusCodes.Status409Conflict, "ABORTED", "The resource changed. Reload and retry.", []);

        if (FindMySqlException(exception) is { } mysqlException)
        {
            return mysqlException.Number switch
            {
                1062 => (StatusCodes.Status409Conflict, "ALREADY_EXISTS", "The requested resource already exists.", []),
                1213 or 1205 => (StatusCodes.Status409Conflict, "ABORTED", "The operation conflicted with another transaction. Please retry.", []),
                1451 or 1452 or 3819 or 4025 => (StatusCodes.Status400BadRequest, "FAILED_PRECONDITION", "The requested data violates a database constraint or relationship.", []),
                _ => (StatusCodes.Status500InternalServerError, "INTERNAL", "A database operation failed.", [])
            };
        }

        var message = environment.IsDevelopment()
            ? exception.Message
            : "An unexpected internal error occurred.";

        return (StatusCodes.Status500InternalServerError, "INTERNAL", message, []);
    }

    private static MySqlException? FindMySqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is MySqlException mysqlException) return mysqlException;
        }
        return null;
    }
}
