using HotelAdmin.Controls;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Khung trang module nhiều tab: thanh tab phía trên, mỗi tab là một control (thường là DataView).
/// Tab chỉ tải dữ liệu khi được mở lần đầu hoặc khi bấm Làm mới.
/// </summary>
public abstract class ModulePage : PageBase
{
    private readonly List<(string Title, Control View)> views = [];
    private readonly HashSet<Control> loaded = [];
    private readonly Panel host = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private SegmentedTabs? tabs;

    protected ModulePage(ApiClient api, UserSession session) : base(api, session)
    {
        Padding = new Padding(24, 0, 24, 16);
        Toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 56, WrapContents = false, BackColor = AppTheme.Background, Padding = new Padding(0, 4, 0, 8)
        };
    }

    /// <summary>Thanh công cụ phía trên (chứa tab; trang có thể thêm control khác).</summary>
    protected FlowLayoutPanel Toolbar { get; }

    /// <summary>Thêm tab. Gọi trong constructor của trang con, sau đó gọi BuildTabs().</summary>
    protected T AddTab<T>(string title, T view) where T : Control
    {
        view.Dock = DockStyle.Fill;
        views.Add((title, view));
        return view;
    }

    /// <summary>Dựng thanh tab sau khi đã thêm đủ tab.</summary>
    protected void BuildTabs()
    {
        tabs = new SegmentedTabs(views.Select(v => v.Title).ToArray()) { Margin = new Padding(0, 0, 12, 0) };
        tabs.SelectedIndexChanged += async (_, _) => await ShowCurrentAsync(false);
        Toolbar.Controls.Add(tabs);
        Toolbar.Controls.SetChildIndex(tabs, 0);
        Controls.Add(host);
        Controls.Add(Toolbar);
        if (views.Count > 0) host.Controls.Add(views[0].View);
    }

    protected Control? CurrentView => tabs is null || views.Count == 0 ? null : views[tabs.SelectedIndex].View;

    public override Task LoadDataAsync() => ShowCurrentAsync(true);

    /// <summary>Tải lại tab đang mở.</summary>
    protected Task ReloadCurrentAsync() => ShowCurrentAsync(true);

    private async Task ShowCurrentAsync(bool force)
    {
        if (CurrentView is not { } view) return;
        if (!host.Controls.Contains(view))
        {
            host.SuspendLayout();
            host.Controls.Clear();
            host.Controls.Add(view);
            host.ResumeLayout();
        }
        if ((force || !loaded.Contains(view)) && view is IReloadable reloadable)
        {
            loaded.Add(view);
            await reloadable.ReloadAsync();
        }
    }
}
