using System.ComponentModel;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>Panel bo góc dùng làm "card" (nền trắng, viền mảnh) giống mẫu.</summary>
public class RoundedPanel : Panel
{
    private Color fillColor = AppTheme.Surface;
    private Color borderColor = AppTheme.Border;
    private int radius = 16;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Padding = new Padding(18);
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color FillColor
    {
        get => fillColor;
        set { fillColor = value; Invalidate(); }
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color BorderColor
    {
        get => borderColor;
        set { borderColor = value; Invalidate(); }
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int Radius
    {
        get => radius;
        set { radius = value; Invalidate(); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Vẽ nền của control cha ở 4 góc, sau đó vẽ khối bo góc.
        e.Graphics.Clear(AppTheme.EffectiveBack(Parent));

        AppTheme.HighQuality(e.Graphics);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = AppTheme.RoundedRect(rect, radius);
        using var brush = new SolidBrush(fillColor);
        e.Graphics.FillPath(brush, path);
        if (borderColor != Color.Transparent)
        {
            using var pen = new Pen(borderColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }
    }
}
