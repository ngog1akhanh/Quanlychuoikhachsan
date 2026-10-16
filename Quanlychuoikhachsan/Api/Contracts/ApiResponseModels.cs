namespace Quanlychuoikhachsan.Api.Contracts;

public sealed record ApiResponse<T>(T Data, string? RequestId = null);

public sealed record ApiAcceptedResponse(string Operation, string? RequestId = null);
