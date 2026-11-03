using HotelAdmin.Controls;
using HotelAdmin.Navigation;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Trang tạm cho module chưa làm giao diện: liệt kê chức năng cần có (theo tài liệu)
/// và API backend đã có / còn thiếu, để nhóm chia việc.
/// </summary>
public sealed class ModulePlaceholderPage : PageBase
{
    private readonly ModuleDef module;

    public ModulePlaceholderPage(ApiClient api, UserSession session, ModuleDef module) : base(api, session)
    {
        this.module = module;
        Padding = new Padding(16, 0, 16, 16);
        BuildUi();
    }

    public override string Title => module.Text;

    private void BuildUi()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background };
        Controls.Add(scroll);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = AppTheme.Background,
            Height = 140 + Math.Max(module.Features.Length * 34 + 90, module.Apis.Length * 34 + 90)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scroll.Controls.Add(layout);

        // Banner mô tả module
        var banner = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            FillColor = AppTheme.Mint,
            BorderColor = Color.Transparent,
            Padding = new Padding(24, 20, 24, 20)
        };
        var icon = new IconBox(module.Glyph) { Location = new Point(24, 26), Size = new Size(52, 52), GlyphSize = 18f, Radius = 14, BorderColor = Color.Transparent };
        var heading = new Label
        {
            Text = $"Module “{module.Text}” đang được xây dựng",
            Font = AppTheme.CardTitle,
            ForeColor = AppTheme.Forest,
            AutoSize = true,
            Location = new Point(92, 24),
            BackColor = Color.Transparent
        };
        var desc = new Label
        {
            Text = module.Description,
            Font = AppTheme.Body,
            ForeColor = AppTheme.TextSoft,
            AutoSize = false,
            Location = new Point(92, 54),
            Size = new Size(900, 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        banner.Controls.AddRange([icon, heading, desc]);
        banner.Resize += (_, _) => desc.Width = Math.Max(200, banner.Width - 120);
        layout.Controls.Add(banner, 0, 0);
        layout.SetColumnSpan(banner, 2);

        // Danh sách chức năng
        var (featureCard, featureBody, _) = CreateCard("Chức năng cần có (theo tài liệu)");
        var featureFlow = NewFlow();
        foreach (var f in module.Features) featureFlow.Controls.Add(Row(Glyph.Check, f, AppTheme.SuccessFg));
        if (module.Features.Length == 0) featureFlow.Controls.Add(Row(Glyph.Info, "Xem trang Tổng quan.", AppTheme.TextMuted));
        featureBody.Controls.Add(featureFlow);
        layout.Controls.Add(featureCard, 0, 1);

        // Danh sách API
        var (apiCard, apiBody, apiHeader) = CreateCard("API backend");
        var legend = new Label
        {
            Text = $"{module.Apis.Count(a => a.Available)} đã có · {module.Apis.Count(a => !a.Available)} cần bổ sung",
            Font = AppTheme.Small,
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };
        apiHeader.Controls.Add(legend);
        var apiFlow = NewFlow();
        foreach (var a in module.Apis)
            apiFlow.Controls.Add(Row(a.Available ? Glyph.Check : Glyph.Warning, a.Route, a.Available ? AppTheme.SuccessFg : AppTheme.WarningFg));
        apiBody.Controls.Add(apiFlow);
        layout.Controls.Add(apiCard, 1, 1);
    }

    private static FlowLayoutPanel NewFlow()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        flow.Resize += (_, _) =>
        {
            foreach (Control c in flow.Controls) c.Width = Math.Max(100, flow.ClientSize.Width - 8);
        };
        return flow;
    }

    private static Control Row(string glyph, string text, Color glyphColor)
    {
        var row = new Panel { Height = 32, Width = 400, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 2) };
        var icon = new Label
        {
            Text = glyph,
            Font = AppTheme.Icon(10f),
            ForeColor = glyphColor,
            AutoSize = false,
            Size = new Size(24, 32),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Left,
            BackColor = Color.Transparent
        };
        var label = new Label
        {
            Text = text,
            Font = AppTheme.Body,
            ForeColor = AppTheme.Text,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        row.Controls.Add(label);
        row.Controls.Add(icon);
        return row;
    }
}
