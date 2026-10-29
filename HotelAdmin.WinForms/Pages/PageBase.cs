using HotelAdmin.Controls;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Lớp cha cho mọi trang nạp vào vùng nội dung của FrmMain.
/// Mỗi thành viên nhóm làm một trang kế thừa lớp này.
/// </summary>
public class PageBase : UserControl
{
    // Constructor rỗng cho Designer của Visual Studio.
    protected PageBase() : this(null!, null!) { }

    protected PageBase(ApiClient api, UserSession session)
    {
        Api = api;
        Session = session;
        DoubleBuffered = true;
        BackColor = AppTheme.Background;
        Dock = DockStyle.Fill;
        Font = AppTheme.Body;
    }

    protected ApiClient Api { get; }
    protected UserSession Session { get; }

    /// <summary>Tiêu đề hiển thị trên header.</summary>
    public virtual string Title => "";

    /// <summary>Nạp dữ liệu (gọi khi mở trang và khi bấm Làm mới).</summary>
    public virtual Task LoadDataAsync() => Task.CompletedTask;

    /// <summary>Tạo card trống có tiêu đề ở góc trên; nội dung đặt trong panel trả về (Dock=Fill).</summary>
    protected static (RoundedPanel Card, Panel Body, Panel HeaderRight) CreateCard(string title, int margin = 8)
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(margin), Padding = new Padding(20, 16, 20, 18) };
        var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
        var titleLabel = new Label
        {
            Text = title,
            Font = AppTheme.CardTitle,
            ForeColor = AppTheme.Text,
            AutoSize = true,
            Location = new Point(0, 4),
            BackColor = Color.Transparent
        };
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        header.Controls.Add(titleLabel);
        header.Controls.Add(right);
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        card.Controls.Add(body);
        card.Controls.Add(header);
        return (card, body, right);
    }
}
