using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Quanlychuoikhachsan.Api.Contracts;
using Quanlychuoikhachsan.Infrastructure.Authentication;
using Quanlychuoikhachsan.Infrastructure.Auditing;
using Quanlychuoikhachsan.Infrastructure.Middleware;
using Quanlychuoikhachsan.Infrastructure.Persistence;
using Quanlychuoikhachsan.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký kết nối MySQL và interceptor ghi nhật ký thay đổi dữ liệu.
var connectionString = builder.Configuration.GetConnectionString("HotelManagement")
    ?? throw new InvalidOperationException("ConnectionStrings:HotelManagement is not configured.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<HotelDbContext>((serviceProvider, options) =>
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>())
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySql =>
    {
        // Các service tự mở transaction Serializable; không bật retry strategy mặc định.
    }));

// Cấu hình xác thực JWT và xử lý lỗi truy cập không xác thực/không đủ quyền.
var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await AuthenticationErrorResponse.WriteAsync(
                    context.Response, StatusCodes.Status401Unauthorized,
                    "UNAUTHENTICATED", "A valid Bearer token is required.");
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await AuthenticationErrorResponse.WriteAsync(
                    context.Response, StatusCodes.Status403Forbidden,
                    "PERMISSION_DENIED", "You do not have permission to perform this operation.");
            }
        };
    });

builder.Services.AddAuthorization();
// Policy quyền được kiểm tra động dựa trên liên kết tài khoản-vai trò-quyền trong database.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    // Chuẩn hóa lỗi validation tự động; API thanh toán giữ payment envelope riêng.
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(pair => pair.Value?.Errors.Count > 0)
            .Select(pair => new GoogleErrorDetail(
                "type.googleapis.com/google.rpc.BadRequest.FieldViolation",
                new
                {
                    field = pair.Key,
                    description = string.Join("; ", pair.Value!.Errors.Select(error => error.ErrorMessage))
                }))
            .ToArray();

        var requestPath = context.HttpContext.Request.Path;
        if (requestPath.StartsWithSegments("/v1/payments/vietqr") ||
            requestPath.Value?.Contains("/vietqr-payment", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new BadRequestObjectResult(
                new Quanlychuoikhachsan.Application.Payments.PaymentApiResponse<object?>(
                    false, "The request contains invalid fields.", details, context.HttpContext.TraceIdentifier));
        }

        return new BadRequestObjectResult(new GoogleErrorResponse(new GoogleError(
            StatusCodes.Status400BadRequest,
            "The request contains invalid fields.",
            "INVALID_ARGUMENT",
            details)));
    };
});

builder.Services.AddControllers(options =>
    {
        // Tên query string dùng camelCase, ví dụ checkInDate.
        options.ModelMetadataDetailsProviders.Add(
            new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.CamelCase));
    })
    .AddJsonOptions(options =>
    {
        // Body và response dùng snake_case; cho phép đọc JSON không phân biệt hoa thường.
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Tạo tài liệu OpenAPI và cấu hình xác thực Bearer cho Swagger.
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel Management API",
        Version = "v1",
        Description = "API for hotel bookings, billing, and inventory management."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token. Swagger will send it as: Bearer {token}."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// Đăng ký Razor Pages, HttpClient và các dịch vụ theo từng phân hệ.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("vietqr", client => client.Timeout = TimeSpan.FromSeconds(30));
// Cấu hình gateway VietQR và service nghiệp vụ theo luồng tạo QR/inquiry.
builder.Services.Configure<Quanlychuoikhachsan.Application.Payments.VietQrSettings>(
    builder.Configuration.GetSection(Quanlychuoikhachsan.Application.Payments.VietQrSettings.SectionName));
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Payments.IVietQrGateway,
    Quanlychuoikhachsan.Infrastructure.Payments.VietQrGateway>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Payments.IVietQrPaymentService,
    Quanlychuoikhachsan.Application.Payments.VietQrPaymentService>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Accounts.IAccountService,
    Quanlychuoikhachsan.Application.Accounts.AccountService>();
builder.Services.AddScoped<IPasswordHasher<TaiKhoan>, PasswordHasher<TaiKhoan>>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Bookings.IBookingService,
    Quanlychuoikhachsan.Application.Bookings.BookingService>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Inventory.IInventoryService,
    Quanlychuoikhachsan.Application.Inventory.InventoryService>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Housekeeping.IHousekeepingService,
    Quanlychuoikhachsan.Application.Housekeeping.HousekeepingService>();
builder.Services.AddScoped<Quanlychuoikhachsan.Application.Reports.IReportService,
    Quanlychuoikhachsan.Application.Reports.ReportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Management API v1");
        options.RoutePrefix = "swagger";
    });
}

// Sắp xếp middleware để định tuyến, chuẩn hóa lỗi và áp dụng xác thực/phân quyền.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
