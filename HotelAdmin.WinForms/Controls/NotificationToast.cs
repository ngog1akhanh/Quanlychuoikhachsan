using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>
/// Popup nhỏ ở góc dưới bên phải màn hình khi có thông báo mới.
/// Tự ẩn sau 8 giây, không lấy focus của cửa sổ đang làm việc; bấm vào để mở hộp thư.
/// </summary>
public sealed class NotificationToast : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 8000 };
    private readonly bool important;

    /// <summary>Người dùng bấm vào popup (để mở hộp thư thông báo).</summary>
    public event EventHandler? Opened;

    public NotificationToast(string type, string title, string content, bool important, int otherUnread)
    {
        this.important = important;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = AppTheme.Surface;
        Size = new Size(380, 112);
        Cursor = Cursors.Hand;

        var header = Labels.Of(type) + (important ? "  ·  Quan trọng" : "")
            + (otherUnread > 0 ? $"  ·  còn {otherUnread} thông báo chưa đọc" : "");
        var lblHeader = new Label
        {
            Text = header, Font = AppTheme.SmallBold, ForeColor = important ? AppTheme.Badge : AppTheme.TextMuted,
            AutoSize = false, AutoEllipsis = true, Location = new Point(20, 10), Size = new Size(Width - 56, 18)
        };
        var lblClose = new Label
        {
            Text = "✕", Font = AppTheme.Small, ForeColor = AppTheme.TextMuted, AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter, Location = new Point(Width - 30, 6), Size = new Size(24, 24)
        };
        var lblTitle = new Label
        {
            Text = title, Font = AppTheme.BodyBold, ForeColor = AppTheme.Text,
            AutoSize = false, AutoEllipsis = true, Location = new Point(20, 32), Size = new Size(Width - 32, 22)
        };
        var lblContent = new Label
        {
            Text = content, Font = AppTheme.Small, ForeColor = AppTheme.TextSoft,
            AutoSize = false, AutoEllipsis = true, Location = new Point(20, 56), Size = new Size(Width - 32, 44)
        };
        Controls.AddRange([lblHeader, lblClose, lblTitle, lblContent]);

        void Open(object? s, EventArgs e) { timer.Stop(); Opened?.Invoke(this, EventArgs.Empty); Close(); }
        Click += Open;
        lblHeader.Click += Open;
        lblTitle.Click += Open;
        lblContent.Click += Open;
        lblClose.Click += (_, _) => Close();
        timer.Tick += (_, _) => Close();
        // Rê chuột vào thì giữ popup, rời chuột thì đếm lại.
        foreach (Control c in Controls) { c.MouseEnter += (_, _) => timer.Stop(); c.MouseLeave += (_, _) => timer.Start(); }
        MouseEnter += (_, _) => timer.Stop();
        MouseLeave += (_, _) => timer.Start();
    }

    /// <summary>Không cướp focus của cửa sổ đang dùng.</summary>
    protected override bool ShowWithoutActivation => true;

    /// <summary>Hiện ở góc dưới phải của màn hình đang chứa cửa sổ chính.</summary>
    public void ShowNear(Form owner)
    {
        var area = Screen.FromControl(owner).WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        Show();
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(AppTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var accent = new SolidBrush(important ? AppTheme.Badge : AppTheme.Lime);
        e.Graphics.FillRectangle(accent, 0, 0, 6, Height);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Dispose();
        base.OnFormClosed(e);
    }
}
