using System.ComponentModel;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Avatar tròn hiển thị chữ cái đầu của tên.</summary>
public class Avatar : Control
{
    public Avatar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(40, 40);
        Font = AppTheme.BodyBold;
    }

    public Color FillColor { get; set; } = AppTheme.Lime;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        using var brush = new SolidBrush(FillColor);
        g.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, AppTheme.Forest,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Ô vuông bo góc chứa icon (dùng trong stat card, header).</summary>
public class IconBox : Control
{
    public IconBox(string glyph)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Glyph = glyph;
        Size = new Size(36, 36);
    }

    public string Glyph { get; set; }
    public Color FillColor { get; set; } = AppTheme.Surface;
    public Color BorderColor { get; set; } = AppTheme.Border;
    public Color GlyphColor { get; set; } = AppTheme.Text;
    public float GlyphSize { get; set; } = 11f;
    public int Radius { get; set; } = 10;
    public bool ShowDot { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = AppTheme.RoundedRect(rect, Radius))
        {
            using var brush = new SolidBrush(FillColor);
            g.FillPath(brush, path);
            if (BorderColor != Color.Transparent)
            {
                using var pen = new Pen(BorderColor);
                g.DrawPath(pen, path);
            }
        }
        TextRenderer.DrawText(g, Glyph, AppTheme.Icon(GlyphSize), ClientRectangle, GlyphColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        if (ShowDot)
        {
            using var dot = new SolidBrush(AppTheme.Badge);
            g.FillEllipse(dot, Width - 12, 6, 7, 7);
        }
    }
}

/// <summary>Nhãn dạng "pill" (ví dụ ▲ 3.5% / trạng thái).</summary>
public class PillLabel : Control
{
    public PillLabel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = AppTheme.SmallBold;
        Height = 22;
    }

    public Color FillColor { get; set; } = AppTheme.Mint;

    public void ApplyStatus(StatusStyle style)
    {
        Text = style.Label;
        FillColor = style.Back;
        ForeColor = style.Fore;
        AutoFit();
    }

    public void AutoFit() => Width = TextRenderer.MeasureText(Text, Font).Width + 16;

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.EffectiveBack(Parent));
        AppTheme.HighQuality(g);
        using var path = AppTheme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f);
        using var brush = new SolidBrush(FillColor);
        g.FillPath(brush, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Ô nhập liệu bo góc có icon phía trước (ô tìm kiếm, ô đăng nhập).</summary>
public class RoundedTextBox : RoundedPanel
{
    private readonly TextBox box;
    private readonly Label? icon;

    public RoundedTextBox(string? glyph = null)
    {
        Radius = 10;
        FillColor = AppTheme.Surface;
        BorderColor = AppTheme.Border;
        Padding = new Padding(glyph is null ? 12 : 36, 0, 12, 0);
        Height = 42;

        box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = AppTheme.Body,
            ForeColor = AppTheme.Text,
            BackColor = AppTheme.Surface,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(box);

        if (glyph is not null)
        {
            icon = new Label
            {
                Text = glyph,
                Font = AppTheme.Icon(10f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = AppTheme.Surface,
                Location = new Point(8, 9)
            };
            Controls.Add(icon);
        }

        box.Enter += (_, _) => { BorderColor = AppTheme.LimeDark; };
        box.Leave += (_, _) => { BorderColor = AppTheme.Border; };
        Click += (_, _) => box.Focus();
    }

    public TextBox Inner => box;

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => box?.Text ?? "";
        set { if (box is not null) box.Text = value ?? ""; }
    }

    public string PlaceholderText
    {
        get => box.PlaceholderText;
        set => box.PlaceholderText = value;
    }

    public bool UseSystemPasswordChar
    {
        get => box.UseSystemPasswordChar;
        set => box.UseSystemPasswordChar = value;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (box is null) return;
        box.Width = Width - Padding.Left - Padding.Right;
        box.Location = new Point(Padding.Left, (Height - box.PreferredHeight) / 2 + 1);
        if (icon is not null) icon.Location = new Point(8, (Height - icon.Height) / 2);
    }
}
