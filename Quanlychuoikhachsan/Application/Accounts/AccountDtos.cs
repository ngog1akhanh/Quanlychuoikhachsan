using System.ComponentModel.DataAnnotations;

namespace Quanlychuoikhachsan.Application.Accounts;

public sealed record RegisterCustomerRequest(
    [param: Required, StringLength(120)] string FullName,
    [param: EmailAddress, StringLength(150)] string? Email,
    [param: Phone, StringLength(20)] string? PhoneNumber,
    [param: Required, MinLength(8), StringLength(128)] string Password,
    DateOnly? BirthDate = null,
    string? Gender = null,
    string? IdentityNumber = null,
    string? Address = null);

public sealed record LoginRequest(
    [param: Required] string Identifier,
    [param: Required] string Password);

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string CustomerName);

public sealed record CustomerResponse(
    string Name,
    long Id,
    string CustomerCode,
    string FullName,
    string? Email,
    string? PhoneNumber,
    DateOnly? BirthDate,
    string? Gender,
    string? IdentityNumber,
    string? Address,
    DateTime CreateTime,
    DateTime UpdateTime);

public sealed record CustomerPageResponse(
    IReadOnlyList<CustomerResponse> Customers,
    string? NextPageToken,
    int TotalSize);

public sealed record UpdateCustomerRequest(
    [param: StringLength(120)] string? FullName = null,
    [param: EmailAddress, StringLength(150)] string? Email = null,
    [param: Phone, StringLength(20)] string? PhoneNumber = null,
    DateOnly? BirthDate = null,
    string? Gender = null,
    string? IdentityNumber = null,
    string? Address = null,
    string? Note = null);

public sealed record StaffResponse(
    string Name,
    long Id,
    string StaffCode,
    string FullName,
    long RoleId,
    string RoleCode,
    string RoleName,
    string? Email,
    string? PhoneNumber,
    string Status,
    DateTime CreateTime);

public sealed record RoleResponse(
    string Name,
    long Id,
    string RoleCode,
    string RoleName,
    string? Description,
    string Status);
