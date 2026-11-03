using System.Drawing.Imaging;
using HotelAdmin.Controls;
using HotelAdmin.Pages;
using HotelAdmin.Services;

// Kiểm tra các trang và tab thật trên STA/message loop; chỉ gọi API đọc sau đăng nhập.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--checkin-input"])
        {
            ApplicationConfiguration.Initialize();
            CheckInInputSmoke.Run();
            return;
        }
        if (args.Length != 2 || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTEL_SMOKE_PASSWORD")))
            throw new ArgumentException("Usage: WinFormsSmoke <api-url> <screenshot-folder>; set HOTEL_SMOKE_IDENTIFIER and HOTEL_SMOKE_PASSWORD.");
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Fail(e.Exception);
        using var host = new Form { Text = "WinForms integration smoke", Width = 1440, Height = 960, ShowInTaskbar = false, Opacity = 0 };
        host.Shown += async (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(args[1]);
                using var handler = new VerifyRequests();
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                var api = new ApiClient(http, new Uri(args[0]));
                var login = await api.LoginAsync(Environment.GetEnvironmentVariable("HOTEL_SMOKE_IDENTIFIER") ?? "admin.com", Environment.GetEnvironmentVariable("HOTEL_SMOKE_PASSWORD")!);
                api.SetToken(login.AccessToken);
                var session = new UserSession(await api.GetMeAsync(), DateTime.UtcNow.AddSeconds(login.ExpiresIn));
                using (var inbox = new HotelAdmin.Forms.Dialogs.NotificationsDialog(api, session) { Opacity = 0 })
                {
                    inbox.Shown += async (_, _) =>
                    {
                        await handler.IdleAsync();
                        using var snapshot = new Bitmap(inbox.Width, inbox.Height);
                        inbox.DrawToBitmap(snapshot, inbox.ClientRectangle);
                        snapshot.Save(Path.Combine(args[1], "NotificationsDialog.png"), ImageFormat.Png);
                        inbox.Close();
                    };
                    inbox.ShowDialog(host);
                    Console.WriteLine("PASS NotificationsDialog");
                }
                var pages = new Func<PageBase>[] {
                    () => new DashboardPage(api, session), () => new RoomsPage(api, session),
                    () => new BookingsPage(api, session), () => new FrontDeskPage(api, session),
                    () => new BillingPage(api, session), () => new HousekeepingPage(api, session),
                    () => new ServicesPage(api, session), () => new CustomersPage(api, session),
                    () => new ReportsPage(api, session), () => new PricingPage(api, session),
                    () => new StaffPage(api, session), () => new InventoryPage(api, session)
                };
                foreach (var create in pages)
                {
                    using var page = create();
                    page.Dock = DockStyle.Fill;
                    host.Controls.Add(page);
                    await page.LoadDataAsync();
                    await handler.IdleAsync();
                    var tabCount = 1;
                    var tabs = Descendants(page).OfType<SegmentedTabs>().FirstOrDefault();
                    if (tabs is not null)
                    {
                        for (var index = 1; index < 32; index++)
                        {
                            tabs.SelectedIndex = index;
                            if (tabs.SelectedIndex != index) break;
                            await page.LoadDataAsync();
                            await handler.IdleAsync();
                            tabCount++;
                        }
                        tabs.SelectedIndex = 0;
                        await handler.IdleAsync();
                    }
                    using var bitmap = new Bitmap(page.Width, page.Height);
                    page.DrawToBitmap(bitmap, page.ClientRectangle);
                    bitmap.Save(Path.Combine(args[1], page.GetType().Name + ".png"), ImageFormat.Png);
                    Console.WriteLine($"PASS {page.GetType().Name}: {tabCount} tabs");
                    host.Controls.Remove(page);
                }
                Console.WriteLine($"PASS: 12 pages; {handler.RequestCount} HTTP requests; {handler.Paths.Count} distinct paths; no HTTP errors.");
            }
            catch (Exception ex) { Fail(ex); }
            finally { host.Close(); }
        };
        Application.Run(host);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Fail(Exception ex)
    {
        Console.Error.WriteLine(ex);
        Environment.Exit(1);
    }

    private sealed class VerifyRequests : DelegatingHandler
    {
        private int active;
        private int count;
        public int RequestCount => count;
        public HashSet<string> Paths { get; } = [];
        public VerifyRequests() : base(new HttpClientHandler { AllowAutoRedirect = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref active);
            Interlocked.Increment(ref count);
            lock (Paths) Paths.Add(request.RequestUri!.AbsolutePath);
            try
            {
                var response = await base.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    Fail(new InvalidOperationException($"{request.Method} {request.RequestUri!.PathAndQuery}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}"));
                return response;
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public async Task IdleAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            do { await Task.Delay(200, timeout.Token); } while (Volatile.Read(ref active) != 0);
        }
    }
}
