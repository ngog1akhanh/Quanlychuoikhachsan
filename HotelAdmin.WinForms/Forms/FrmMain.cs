using System.Globalization;
using HotelAdmin.Controls;
using HotelAdmin.Navigation;
using HotelAdmin.Pages;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms;

/// <summary>
/// Khung chính của app: sidebar (menu theo vai trò) + header (tiêu đề, tìm kiếm, thông báo, người dùng)
/// + vùng nội dung nạp các trang (UserControl kế thừa PageBase).
/// </summary>
public sealed class FrmMain : Form
{
    private static readonly CultureInfo Vi = new("vi-VN");

    private readonly ApiClient api;
    private readonly UserSession session;
    private readonly AppConfig config;

    private readonly Panel sidebar = new();
    private readonly FlowLayoutPanel menu = new();
    private readonly Panel header = new();
    private readonly Panel content = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();
    private readonly List<SidebarButton> menuButtons = [];

    private PageBase? currentPage;
    private bool sessionExpiredHandled;

    public FrmMain(ApiClient api, UserSession session, AppConfig config)
    {
        this.api = api;
        this.session = session;
        this.config = config;

        Text = $"{config.HotelName} — App quản lý";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1480, 920);
        MinimumSize = new Size(1200, 720);
        WindowState = FormWindowState.Maximized;
        BackColor = AppTheme.Background;
        Font = AppTheme.Body;
        DoubleBuffered = true;

        BuildSidebar();
        BuildHeader();
        content.Dock = DockStyle.Fill;
        content.BackColor = AppTheme.Background;

        // Thứ tự add: Fill trước, sau đó Top, cuối cùng Left (để sidebar chiếm toàn chiều cao).
        var main = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
        main.Controls.Add(content);
        main.Controls.Add(header);
        Controls.Add(main);
        Controls.Add(sidebar);

        api.SessionExpired += OnSessionExpired;
        Shown += async (_, _) => await NavigateAsync(Modules.Dashboard);
    }

    /// <summary>true khi người dùng bấm Đăng xuất (Program sẽ mở lại màn hình đăng nhập).</summary>
    public bool LogoutRequested { get; private set; }

    // ======================================================================
    // Sidebar
    // ======================================================================
    private void BuildSidebar()
    {
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 250;
        sidebar.BackColor = AppTheme.Surface;
        sidebar.Padding = new Padding(18, 0, 18, 18);
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        var logo = new LogoMark { Text = config.HotelName, Dock = DockStyle.Top, Height = 84 };

        menu.Dock = DockStyle.Fill;
        menu.FlowDirection = FlowDirection.TopDown;
        menu.WrapContents = false;
        menu.AutoScroll = true;
        menu.BackColor = AppTheme.Surface;
        menu.Padding = new Padding(0, 0, 0, 8);

        string? lastGroup = null;
        foreach (var module in Modules.All.Where(m => m.Roles.Length == 0 || session.HasAnyRole(m.Roles)))
        {
            if (module.Group != lastGroup)
            {
                menu.Controls.Add(new Label
                {
                    Text = module.Group,
                    Font = AppTheme.SmallBold,
                    ForeColor = AppTheme.TextMuted,
                    AutoSize = false,
                    Size = new Size(210, 30),
                    TextAlign = ContentAlignment.BottomLeft,
                    Margin = new Padding(4, lastGroup is null ? 0 : 10, 0, 4)
                });
                lastGroup = module.Group;
            }

            var button = new SidebarButton(module.Glyph, module.Text)
            {
                PageKey = module.Key,
                Width = 212,
                Margin = new Padding(0, 0, 0, 2)
            };
            button.Click += async (_, _) => await NavigateAsync(module.Key);
            menuButtons.Add(button);
            menu.Controls.Add(button);
        }

        var sessionCard = BuildSessionCard();
        sidebar.Controls.Add(menu);
        sidebar.Controls.Add(sessionCard);
        sidebar.Controls.Add(logo);
    }

    /// <summary>Card cuối sidebar: thông tin vai trò + nút đăng xuất (thay khối quảng cáo của mẫu).</summary>
    private Control BuildSessionCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            FillColor = AppTheme.Mint,
            BorderColor = Color.Transparent,
            Padding = new Padding(16)
        };
        var role = new Label
        {
            Text = session.RoleTitle,
            Font = AppTheme.BodyBold,
            ForeColor = AppTheme.Forest,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(16, 16),
            Size = new Size(180, 22),
            BackColor = Color.Transparent
        };
        var perms = new Label
        {
            Text = $"{session.Me.Permissions.Count} quyền · mã NV {session.Me.StaffCode ?? "-"}",
            Font = AppTheme.Small,
            ForeColor = AppTheme.TextSoft,
            AutoSize = false,
            Location = new Point(16, 40),
            Size = new Size(180, 40),
            BackColor = Color.Transparent
        };
        var logout = new FlatButton
        {
            Text = "Đăng xuất",
            IconGlyph = Glyph.Power,
            FillColor = AppTheme.Lime,
            Location = new Point(16, 92),
            Size = new Size(150, 40)
        };
        logout.Click += (_, _) => Logout();
        card.Controls.AddRange([role, perms, logout]);
        return card;
    }

    // ======================================================================
    // Header
    // ======================================================================
    private void BuildHeader()
    {
        header.Dock = DockStyle.Top;
        header.Height = 92;
        header.BackColor = AppTheme.Background;
        header.Padding = new Padding(24, 18, 24, 10);

        lblTitle.Font = AppTheme.PageTitle;
        lblTitle.ForeColor = AppTheme.Text;
        lblTitle.AutoSize = true;
        lblTitle.Location = new Point(24, 14);

        lblSubtitle.Font = AppTheme.Small;
        lblSubtitle.ForeColor = AppTheme.TextMuted;
        lblSubtitle.AutoSize = true;
        lblSubtitle.Location = new Point(27, 56);

        // Cụm bên phải: tìm module · làm mới · thông báo · người dùng
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = AppTheme.Background,
            Padding = new Padding(0, 4, 0, 0)
        };

        var search = new RoundedTextBox(Glyph.Search) { Width = 280, Height = 42, PlaceholderText = "Tìm chức năng (vd: hóa đơn, kho)...", Margin = new Padding(0, 0, 14, 0) };
        search.Inner.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            var q = search.Text.Trim();
            var match = menuButtons.FirstOrDefault(b => b.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase));
            if (q.Length > 0 && match is not null) await NavigateAsync(match.PageKey);
        };

        var refresh = HeaderIcon(Glyph.Refresh, "Làm mới dữ liệu");
        refresh.Click += async (_, _) => await ReloadCurrentAsync();

        var bell = HeaderIcon(Glyph.Bell, "Thông báo nội bộ");
        bell.ShowDot = true;
        bell.Click += (_, _) => MessageBox.Show(this,
            "Thông báo nội bộ (mục 17.2): booking mới, yêu cầu hủy, phòng cần dọn, sự cố, tồn kho thấp.\n\n" +
            "Backend chưa có API thông báo. Đề xuất: bảng thong_bao + SignalR hub để đẩy realtime.",
            "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

        var avatar = new Avatar { Text = session.Initials, Size = new Size(42, 42), Margin = new Padding(12, 0, 8, 0) };
        var who = new Panel { Size = new Size(190, 44), BackColor = AppTheme.Background, Margin = new Padding(0) };
        who.Controls.Add(new Label { Text = session.DisplayName, Font = AppTheme.BodyBold, ForeColor = AppTheme.Text, AutoSize = false, AutoEllipsis = true, Location = new Point(0, 2), Size = new Size(190, 20) });
        who.Controls.Add(new Label { Text = session.RoleTitle, Font = AppTheme.Small, ForeColor = AppTheme.TextMuted, AutoSize = false, AutoEllipsis = true, Location = new Point(0, 23), Size = new Size(190, 18) });

        var userMenu = new ContextMenuStrip();
        userMenu.Items.Add($"Đăng nhập: {session.Me.Email ?? session.DisplayName}").Enabled = false;
        userMenu.Items.Add(new ToolStripSeparator());
        userMenu.Items.Add("Đăng xuất", null, (_, _) => Logout());
        avatar.Click += (_, _) => userMenu.Show(avatar, new Point(0, avatar.Height + 4));
        who.Click += (_, _) => userMenu.Show(who, new Point(0, who.Height + 4));
        avatar.Cursor = Cursors.Hand;

        right.Controls.AddRange([search, refresh, bell, avatar, who]);
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSubtitle);
        header.Controls.Add(right);
    }

    private static IconBox HeaderIcon(string glyph, string tooltip)
    {
        var icon = new IconBox(glyph)
        {
            Size = new Size(42, 42),
            Radius = 12,
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            GlyphSize = 11f
        };
        new ToolTip().SetToolTip(icon, tooltip);
        return icon;
    }

    // ======================================================================
    // Điều hướng
    // ======================================================================
    private async Task NavigateAsync(string key)
    {
        var module = Modules.All.First(m => m.Key == key);
        foreach (var b in menuButtons) b.Selected = b.PageKey == key;

        // Map module -> trang. Module chưa làm vẫn dùng trang tạm (liệt kê chức năng + API còn thiếu).
        PageBase page = key switch
        {
            Modules.Dashboard => new DashboardPage(api, session),
            "rooms" => new RoomsPage(api, session),          // Giai đoạn 1A
            "bookings" => new BookingsPage(api, session),    // Giai đoạn 1B
            "frontdesk" => new FrontDeskPage(api, session),  // Giai đoạn 1C
            _ => new ModulePlaceholderPage(api, session, module)
        };

        content.SuspendLayout();
        var old = currentPage;
        currentPage = page;
        content.Controls.Add(page);
        if (old is not null)
        {
            content.Controls.Remove(old);
            old.Dispose();
        }
        content.ResumeLayout();

        lblTitle.Text = page.Title;
        await ReloadCurrentAsync();
    }

    private async Task ReloadCurrentAsync()
    {
        if (currentPage is null) return;
        var page = currentPage;
        lblSubtitle.Text = "Đang tải dữ liệu...";
        UseWaitCursor = true;
        try
        {
            await page.LoadDataAsync();
            if (page == currentPage) lblSubtitle.Text = SubtitleText();
        }
        catch (ApiException ex) when (!ex.IsUnauthorized)
        {
            lblSubtitle.Text = SubtitleText();
            MessageBox.Show(this, ex.Message, "Không tải được dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (ApiException)
        {
            // 401 đã được xử lý qua sự kiện SessionExpired.
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static string SubtitleText()
    {
        var now = DateTime.Now;
        return $"{now.ToString("dddd, dd/MM/yyyy", Vi)} · cập nhật lúc {now:HH:mm}";
    }

    // ======================================================================
    // Phiên đăng nhập
    // ======================================================================
    private void OnSessionExpired(object? sender, EventArgs e)
    {
        if (sessionExpiredHandled || IsDisposed) return;
        sessionExpiredHandled = true;
        BeginInvoke(new Action(() =>
        {
            MessageBox.Show(this, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", "Hết phiên",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LogoutRequested = true;
            Close();
        }));
    }

    private void Logout()
    {
        if (MessageBox.Show(this, "Bạn muốn đăng xuất?", "Đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        LogoutRequested = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        api.SessionExpired -= OnSessionExpired;
        base.OnFormClosed(e);
    }
}
