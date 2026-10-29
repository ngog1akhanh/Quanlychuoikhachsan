using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Nút bo góc phẳng (màu xanh chanh mặc định), có thể kèm icon glyph.</summary>
public class FlatButton : Control, IButtonControl
{
    private bool hover;
    private bool pressed;
    private bool isDefault;

    public FlatButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        ForeColor = AppTheme.Text;
        Font = AppTheme.BodyBold;
        Cursor = Cursors.Hand;
        Size = new Size(120, 40);
    }

    public Color FillColor { get; set; } = AppTheme.Lime;
    public Color HoverColor { get; set; } = AppTheme.LimeHover;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int Radius { get; set; } = 10;
    public string? IconGlyph { get; set; }
    public float IconSize { get; set; } = 10f;
    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value)
    {
        isDefault = value;
        Invalidate();
    }

    public void PerformClick()
    {
        if (CanSelect || Enabled) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        if (!Enabled) return;
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is { } form) form.DialogResult = DialogResult;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) PerformClick();
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        AppTheme.HighQuality(g);
        g.Clear(AppTheme.EffectiveBack(Parent));

        var fill = !Enabled ? AppTheme.NeutralBg : hover || pressed ? HoverColor : FillColor;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (pressed) rect.Inflate(-1, -1);
        using (var path = AppTheme.RoundedRect(rect, Radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
            var border = Focused && isDefault ? AppTheme.LimeDark : BorderColor;
            if (border != Color.Transparent)
            {
                using var pen = new Pen(border);
                g.DrawPath(pen, path);
            }
        }

        var fore = Enabled ? ForeColor : AppTheme.TextMuted;
        var textSize = TextRenderer.MeasureText(g, Text, Font);
        var iconWidth = 0;
        if (!string.IsNullOrEmpty(IconGlyph))
            iconWidth = TextRenderer.MeasureText(g, IconGlyph, AppTheme.Icon(IconSize)).Width + (string.IsNullOrEmpty(Text) ? 0 : 2);

        var totalWidth = iconWidth + (string.IsNullOrEmpty(Text) ? 0 : textSize.Width);
        var x = (Width - totalWidth) / 2;
        if (iconWidth > 0)
        {
            TextRenderer.DrawText(g, IconGlyph, AppTheme.Icon(IconSize), new Rectangle(x, 0, iconWidth, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            x += iconWidth;
        }
        if (!string.IsNullOrEmpty(Text))
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textSize.Width, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
