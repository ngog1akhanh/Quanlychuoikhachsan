using QuanLyKhachSan.Web;
using QuanLyKhachSan.Web.Services;
using QuanLyKhachSan.Web.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---- Cấu hình API backend (appsettings.json -> "Api") ----
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
var apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();

// ---- State (mỗi kết nối Blazor = 1 scope) ----
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<BookingState>();

// ---- Services gọi API ----
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<HotelInfoService>();

var app = builder.Build();

RoomImages.Init(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
