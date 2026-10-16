using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Quanlychuoikhachsan.Application.Common;
using Quanlychuoikhachsan.Application.Payments;

namespace Quanlychuoikhachsan.Infrastructure.Payments;

/// <summary>Gọi các endpoint VietQR sandbox để tạo QR và tra cứu giao dịch bằng Bearer token.</summary>
public sealed class VietQrGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<VietQrSettings> settings,
    ILogger<VietQrGateway> logger) : IVietQrGateway
{
    private const string TokenEndpoint = "https://api.vietqr.org/vqr/api/token_generate";
    private const string GenerateQrEndpoint = "https://api.vietqr.org/vqr/api/qr/generate-customer";
    private const string InquiryEndpoint = "https://api.vietqr.org/vqr/api/qr/inquiry";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VietQrSettings vietQrSettings = settings.Value;

    // Tạo Bearer token, gọi API tạo QR và chỉ trả kết quả đủ mã QR cùng ảnh QR.
    public async Task<VietQrCustomerQrResult> CreateCustomerQrAsync(
        VietQrCreateCustomerQrRequest request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var client = httpClientFactory.CreateClient("vietqr");
        using var qrRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            GenerateQrEndpoint,
            request,
            client,
            cancellationToken);
        var response = await SendAsync<VietQrApiResponse<VietQrQrData>>(
            client, qrRequest, cancellationToken);

        if (!string.Equals(response.Code, "00", StringComparison.Ordinal) ||
            response.Data is null ||
            string.IsNullOrWhiteSpace(response.Data.QrCode) ||
            string.IsNullOrWhiteSpace(response.Data.QrDataUrl))
        {
            logger.LogWarning("VietQR sandbox returned an unsuccessful or incomplete QR response with code {Code}.", response.Code);
            throw new ServiceUnavailableException("VietQR sandbox did not return a usable QR code.");
        }

        return new VietQrCustomerQrResult(response.Data.QrCode, response.Data.QrDataUrl);
    }

    // Gọi POST Inquiry theo orderId và chuẩn hóa dữ liệu giao dịch trả về từ VietQR.
    public async Task<VietQrInquiryResult> InquireAsync(string orderId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var client = httpClientFactory.CreateClient("vietqr");
        using var inquiryRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            InquiryEndpoint,
            new VietQrInquiryRequest(orderId),
            client,
            cancellationToken);
        var response = await SendAsync<VietQrApiResponse<VietQrInquiryData>>(
            client, inquiryRequest, cancellationToken);

        var responseCode = response.Code;
        if (responseCode is null ||
            !string.Equals(responseCode, "00", StringComparison.Ordinal) ||
            response.Data is null ||
            string.IsNullOrWhiteSpace(response.Data.OrderId) ||
            string.IsNullOrWhiteSpace(response.Data.Status))
        {
            logger.LogWarning("VietQR sandbox returned an unsuccessful or incomplete inquiry for order {OrderId} with code {Code}.", orderId, response.Code);
            throw new ServiceUnavailableException("VietQR sandbox did not return a usable inquiry result.");
        }

        return new VietQrInquiryResult(
            responseCode,
            response.Description ?? string.Empty,
            response.Data.OrderId,
            response.Data.Amount,
            response.Data.TransactionId,
            response.Data.Status,
            response.Data.TransactionDescription,
            response.Data.TransactionDateTime);
    }

    // Tạo yêu cầu có Bearer token dùng chung cho các endpoint VietQR cần xác thực.
    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync<TBody>(
        HttpMethod method,
        string endpoint,
        TBody body,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(client, cancellationToken);
        var request = new HttpRequestMessage(method, endpoint)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // Lấy access token bằng cặp thông tin Basic Authentication của tài khoản sandbox.
    private async Task<string> GetAccessTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{vietQrSettings.Username}:{vietQrSettings.Password}"));
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await SendAsync<VietQrTokenResponse>(client, tokenRequest, cancellationToken);
        if (string.IsNullOrWhiteSpace(response.AccessToken) ||
            !string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("VietQR sandbox returned an invalid token response.");
            throw new ServiceUnavailableException("VietQR sandbox authentication failed.");
        }

        return response.AccessToken;
    }

    // Chuẩn hóa lỗi HTTP, timeout và JSON thành lỗi dịch vụ ổn định cho API.
    private async Task<TResponse> SendAsync<TResponse>(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("VietQR sandbox returned HTTP status {StatusCode}.", (int)response.StatusCode);
                throw new ServiceUnavailableException("VietQR sandbox request failed.");
            }

            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
            return result ?? throw new ServiceUnavailableException("VietQR sandbox returned an empty response.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach VietQR sandbox.");
            throw new ServiceUnavailableException("VietQR sandbox is temporarily unavailable.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "VietQR sandbox request timed out.");
            throw new ServiceUnavailableException("VietQR sandbox request timed out.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "VietQR sandbox returned invalid JSON.");
            throw new ServiceUnavailableException("VietQR sandbox returned an invalid response.");
        }
    }

    // Không gửi request nếu thiếu credentials hoặc thông tin tài khoản nhận tiền phía server.
    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(vietQrSettings.Username) ||
            string.IsNullOrWhiteSpace(vietQrSettings.Password) ||
            string.IsNullOrWhiteSpace(vietQrSettings.BankAccount) ||
            string.IsNullOrWhiteSpace(vietQrSettings.BankCode) ||
            string.IsNullOrWhiteSpace(vietQrSettings.UserBankName))
        {
            throw new ServiceUnavailableException("VietQR sandbox credentials and receiving-account settings must be configured.");
        }
    }

    private sealed record VietQrTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record VietQrApiResponse<TData>(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("desc")] string? Description,
        [property: JsonPropertyName("data")] TData? Data);

    private sealed record VietQrQrData(
        [property: JsonPropertyName("qrCode")] string? QrCode,
        [property: JsonPropertyName("qrDataURL")] string? QrDataUrl);

    private sealed record VietQrInquiryRequest(
        [property: JsonPropertyName("orderId")] string OrderId);

    private sealed record VietQrInquiryData(
        [property: JsonPropertyName("orderId")] string? OrderId,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("transactionId")] string? TransactionId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("description")] string? TransactionDescription,
        [property: JsonPropertyName("transactionDateTime")] string? TransactionDateTime);
}
