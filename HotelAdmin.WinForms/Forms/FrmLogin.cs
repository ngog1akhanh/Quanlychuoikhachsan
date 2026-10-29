using System.Net;
using System.Runtime.InteropServices;
using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms;

/// <summary>
/// Màn hình đăng nhập nhân viên: gọi POST /v1/auth/login lấy JWT, sau đó GET /v1/me
/// để lấy vai trò/quyền. Tài khoản khách hàng (KHACH_HANG) bị từ chối.
/// </summary>
public sealed class FrmLogin : Form
{
    private readonly ApiClient api;
    private readonly AppConfig config;
    private readonly RoundedTextBox txtUser = new(Glyph.User) { PlaceholderText = "Email hoặc số điện thoại" };
    private readonly RoundedTextBox txtPass = new(Glyph.Lock) { PlaceholderText = "Mật khẩu", UseSystemPasswordChar = true };
    private readonly CheckBox chkShow = new();
    private readonly Label lblError = new();
    private readonly FlatButton btnLogin = new() { Text = "Đăng nhập", Height = 46 };

    public FrmLogin(ApiClient api, AppConfig config)
    {
        this.api = api;
        this.config = config;

        Text = $"{config.HotelName} — Đăng nhập";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 600);
        BackColor = AppTheme.Surface;
        Font = AppTheme.Body;
        DoubleBuffered = true;
        KeyPreview = true;

        BuildUi();
        AcceptButton = btnLogin;
        Shown += (_, _) => txtUser.Inner.Focus();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    /// <summary>Phiên đăng nhập thành công (null nếu người dùng đóng form).</summary>
    public UserSession? Session { get; private set; }

    private void BuildUi()
    {
        // ---- Nửa trái: khối thương hiệu ----
        var brand = new BrandPanel(config.HotelName) { Dock = DockStyle.Left, Width = 430 };
        brand.MouseDown += DragForm;

        // ---- Nửa phải: form ----
        var right = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, Padding = new Padding(70, 40, 70, 30) };
        right.MouseDown += DragForm;

        var close = new IconBox("") { Size = new Size(34, 34), BorderColor = Color.Transparent, GlyphSize = 9f, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        close.Location = new Point(980 - 430 - 46, 12);
        close.Click += (_, _) => Close();

        var title = new Label { Text = "Đăng nhập", Font = AppTheme.PageTitle, ForeColor = AppTheme.Text, AutoSize = true, Location = new Point(70, 110) };
        var sub = new Label
        {
            Text = "Dành cho lễ tân, buồng phòng, dịch vụ, kho, quản lý và admin.",
            Font = AppTheme.Body,
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(72, 156)
        };

        var lblUser = FieldLabel("Email / Số điện thoại", 205);
        txtUser.SetBounds(70, 230, 410, 46);
        var lblPass = FieldLabel("Mật khẩu", 292);
        txtPass.SetBounds(70, 317, 410, 46);

        chkShow.Text = "Hiện mật khẩu";
        chkShow.AutoSize = true;
        chkShow.ForeColor = AppTheme.TextSoft;
        chkShow.Location = new Point(70, 374);
        chkShow.CheckedChanged += (_, _) => txtPass.UseSystemPasswordChar = !chkShow.Checked;

        lblError.AutoSize = false;
        lblError.ForeColor = AppTheme.DangerFg;
        lblError.Font = AppTheme.Small;
        lblError.SetBounds(70, 402, 410, 40);

        btnLogin.SetBounds(70, 446, 410, 48);
        btnLogin.IconGlyph = "";
        btnLogin.Click += async (_, _) => await LoginAsync();

        var server = new Label
        {
            Text = $"Máy chủ API: {api.BaseUrl}  (sửa trong appsettings.json)",
            Font = AppTheme.Small,
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(70, 540)
        };

        right.Controls.AddRange([close, title, sub, lblUser, txtUser, lblPass, txtPass, chkShow, lblError, btnLogin, server]);
        Controls.Add(right);
        Controls.Add(brand);
    }

    private static Label FieldLabel(string text, int top) => new()
    {
        Text = text,
        Font = AppTheme.SmallBold,
        ForeColor = AppTheme.TextSoft,
        AutoSize = true,
        Location = new Point(70, top)
    };

    private async Task LoginAsync()
    {
        if (!btnLogin.Enabled) return; // đang xử lý, tránh bấm 2 lần
        var user = txtUser.Text.Trim();
        var pass = txtPass.Text;
        if (user.Length == 0 || pass.Length == 0)
        {
            lblError.Text = "Vui lòng nhập tài khoản và mật khẩu.";
            return;
        }

        SetBusy(true);
        try
        {
            var token = await api.LoginAsync(user, pass);
            api.SetToken(token.AccessToken);

            MeResponse me;
            try
            {
                me = await api.GetMeAsync();
            }
            catch (ApiException ex) when (ex.IsUnauthorized)
            {
                api.ClearToken();
                lblError.Text = "Mật khẩu đúng nhưng API /v1/me từ chối token (401). Kiểm tra cấu hình Jwt và địa chỉ API.";
                return;
            }
            if (!string.Equals(me.AccountType, "NHAN_VIEN", StringComparison.OrdinalIgnoreCase))
            {
                api.ClearToken();
                lblError.Text = "Tài khoản khách hàng không dùng được app quản lý. Hãy dùng tài khoản nhân viên.";
                return;
            }

            Session = new UserSession(me, DateTime.Now.AddSeconds(token.ExpiresIn));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            api.ClearToken();
            lblError.Text = ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Sai tài khoản hoặc mật khẩu.",
                HttpStatusCode.NotFound => "Backend chưa có API GET /v1/me (thêm MeController.cs vào project API).",
                _ => ex.Message
            };
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        btnLogin.Enabled = !busy;
        btnLogin.Text = busy ? "Đang đăng nhập..." : "Đăng nhập";
        UseWaitCursor = busy;
        if (busy) lblError.Text = "";
    }

    // ---- Kéo form không viền ----
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void DragForm(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    /// <summary>Khối thương hiệu bên trái: nền mint, logo, khẩu hiệu và hình khối trang trí.</summary>
    private sealed class BrandPanel : Panel
    {
        private static readonly Font Headline = new("Segoe UI Semibold", 22f);
        private readonly string hotelName;

        public BrandPanel(string hotelName)
        {
            this.hotelName = hotelName;
            DoubleBuffered = true;
            BackColor = AppTheme.Mint;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            AppTheme.HighQuality(g);

            // Hình khối trang trí
            using (var lime = new SolidBrush(Color.FromArgb(160, AppTheme.Lime)))
                g.FillEllipse(lime, Width - 220, Height - 260, 380, 380);
            using (var soft = new SolidBrush(Color.FromArgb(110, Color.White)))
                g.FillEllipse(soft, -120, -140, 320, 320);

            // Toà nhà cách điệu
            var baseY = Height - 70f;
            Building(g, 60, baseY, 70, 190, AppTheme.Surface);
            Building(g, 140, baseY, 90, 260, AppTheme.LimeDark);
            Building(g, 240, baseY, 64, 150, AppTheme.MintDark);
            using (var ground = new Pen(AppTheme.Forest, 2f))
                g.DrawLine(ground, 40, baseY, Width - 40, baseY);

            // Logo + chữ
            var y = 48;
            Bar(g, 48, y + 12, 8, 16, AppTheme.MintDark);
            Bar(g, 59, y + 2, 8, 26, AppTheme.LimeDark);
            Bar(g, 70, y + 7, 8, 21, AppTheme.Surface);
            TextRenderer.DrawText(g, hotelName, AppTheme.Logo, new Point(86, y + 1), AppTheme.Forest);
            TextRenderer.DrawText(g, "Hệ thống quản lý\nkhách sạn", Headline, new Rectangle(46, 110, Width - 80, 90), AppTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(g, "Đặt phòng · Lễ tân · Buồng phòng · Hóa đơn · Kho · Báo cáo", AppTheme.Body,
                new Rectangle(48, 200, Width - 80, 40), AppTheme.TextSoft, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        private static void Building(Graphics g, float x, float baseY, float w, float h, Color color)
        {
            var rect = new RectangleF(x, baseY - h, w, h);
            using (var path = AppTheme.RoundedRect(rect, 8))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);
            using var win = new SolidBrush(Color.FromArgb(90, AppTheme.Forest));
            for (var wy = rect.Top + 16; wy < baseY - 24; wy += 22)
                for (var wx = rect.Left + 12; wx < rect.Right - 14; wx += 18)
                    g.FillRectangle(win, wx, wy, 8, 10);
        }

        private static void Bar(Graphics g, float x, float y, float w, float h, Color color)
        {
            using var path = AppTheme.RoundedRect(new RectangleF(x, y, w, h), 2);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }
    }
}
