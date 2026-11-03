using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HotelAdmin.Theme;

/// <summary>Bảng màu, font và icon dùng chung (lấy từ mẫu giao diện Lodgify).</summary>
public static class AppTheme
{
    // ---- Màu nền ----
    public static readonly Color Background = ColorTranslator.FromHtml("#F4F6F2");
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = ColorTranslator.FromHtml("#E9ECE6");
    public static readonly Color Hover = ColorTranslator.FromHtml("#F3F6EE");

    // ---- Màu nhấn ----
    public static readonly Color Lime = ColorTranslator.FromHtml("#E7F68F");
    public static readonly Color LimeHover = ColorTranslator.FromHtml("#DDEF73");
    public static readonly Color LimeDark = ColorTranslator.FromHtml("#C5D86A");
    public static readonly Color Mint = ColorTranslator.FromHtml("#D5F6E5");
    public static readonly Color MintSoft = ColorTranslator.FromHtml("#EAFBF2");
    public static readonly Color MintDark = ColorTranslator.FromHtml("#A9DCC2");
    public static readonly Color Sage = ColorTranslator.FromHtml("#B9D3C4");
    public static readonly Color Forest = ColorTranslator.FromHtml("#2F5D45");

    // ---- Chữ ----
    public static readonly Color Text = ColorTranslator.FromHtml("#1D2420");
    public static readonly Color TextMuted = ColorTranslator.FromHtml("#8A928C");
    public static readonly Color TextSoft = ColorTranslator.FromHtml("#5E6761");

    // ---- Trạng thái ----
    public static readonly Color SuccessBg = ColorTranslator.FromHtml("#D5F6E5");
    public static readonly Color SuccessFg = ColorTranslator.FromHtml("#23784D");
    public static readonly Color WarningBg = ColorTranslator.FromHtml("#FFF2C6");
    public static readonly Color WarningFg = ColorTranslator.FromHtml("#946B00");
    public static readonly Color DangerBg = ColorTranslator.FromHtml("#FDE1E1");
    public static readonly Color DangerFg = ColorTranslator.FromHtml("#C93B3B");
    public static readonly Color InfoBg = ColorTranslator.FromHtml("#E2ECFF");
    public static readonly Color InfoFg = ColorTranslator.FromHtml("#3563C9");
    public static readonly Color NeutralBg = ColorTranslator.FromHtml("#EEF0EC");
    public static readonly Color NeutralFg = ColorTranslator.FromHtml("#5E6761");
    public static readonly Color Badge = ColorTranslator.FromHtml("#F04438");

    // ---- Font ----
    private const string UiFamily = "Segoe UI";
    public static readonly Font Body = new(UiFamily, 9.75f);
    public static readonly Font BodyBold = new(UiFamily, 9.75f, FontStyle.Bold);
    public static readonly Font Small = new(UiFamily, 8.5f);
    public static readonly Font SmallBold = new(UiFamily, 8.5f, FontStyle.Bold);
    public static readonly Font CardTitle = new("Segoe UI Semibold", 11.5f);
    public static readonly Font PageTitle = new("Segoe UI Semibold", 19f);
    public static readonly Font Kpi = new("Segoe UI Semibold", 20f);
    public static readonly Font KpiSmall = new("Segoe UI Semibold", 15f);
    public static readonly Font Logo = new("Segoe UI Semibold", 15f);

    // ---- Icon font (Segoe Fluent Icons trên Win11, Segoe MDL2 Assets trên Win10) ----
    private static readonly string IconFamily = ResolveIconFamily();
    private static readonly Dictionary<float, Font> IconCache = new();

    public static Font Icon(float size)
    {
        if (!IconCache.TryGetValue(size, out var font))
        {
            font = new Font(IconFamily, size, FontStyle.Regular, GraphicsUnit.Point);
            IconCache[size] = font;
        }
        return font;
    }

    private static string ResolveIconFamily()
    {
        using var fonts = new InstalledFontCollection();
        var names = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Contains("Segoe Fluent Icons")) return "Segoe Fluent Icons";
        if (names.Contains("Segoe MDL2 Assets")) return "Segoe MDL2 Assets";
        return UiFamily;
    }

    // ---- Tiện ích vẽ ----
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 1)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Màu nền thực tế phía sau một control (bỏ qua các lớp Transparent, lấy FillColor của card).</summary>
    public static Color EffectiveBack(Control? control)
    {
        while (control is not null)
        {
            if (control is Controls.RoundedPanel rp) return rp.FillColor;
            if (control.BackColor != Color.Transparent && control.BackColor.A == 255) return control.BackColor;
            control = control.Parent;
        }
        return Background;
    }

    public static void HighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static string Money(decimal value)
    {
        if (Math.Abs(value) >= 1_000_000_000) return (value / 1_000_000_000m).ToString("0.##") + " tỷ";
        if (Math.Abs(value) >= 1_000_000) return (value / 1_000_000m).ToString("0.#") + " tr";
        return value.ToString("#,0") + " đ";
    }

    public static string MoneyFull(decimal value) => value.ToString("#,0") + " đ";
}

/// <summary>Mã glyph của Segoe MDL2 Assets / Segoe Fluent Icons.</summary>
public static class Glyph
{
    public const string Home = "";
    public const string Calendar = "";
    public const string Switch = "";
    public const string Rooms = "";
    public const string Calculator = "";
    public const string Shop = "";
    public const string Card = "";
    public const string Contact = "";
    public const string Check = "";
    public const string Package = "";
    public const string People = "";
    public const string Chart = "";
    public const string Bell = "";
    public const string Settings = "";
    public const string Search = "";
    public const string Power = "";
    public const string Refresh = "";
    public const string Permissions = "";
    public const string Info = "";
    public const string Warning = "";
    public const string ArrowIn = "";
    public const string ArrowOut = "";
    public const string Money = "";
    public const string Bed = "";
    public const string Down = "";
    public const string Clock = "";
    public const string Lock = "";
    public const string User = "";
}
