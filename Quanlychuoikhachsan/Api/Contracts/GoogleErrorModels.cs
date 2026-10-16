namespace Quanlychuoikhachsan.Api.Contracts;

public sealed record GoogleErrorResponse(GoogleError Error);

public sealed record GoogleError(
    int Code,
    string Message,
    string Status,
    IReadOnlyList<GoogleErrorDetail> Details);

public sealed record GoogleErrorDetail(
    string Type,
    object? Metadata = null);
