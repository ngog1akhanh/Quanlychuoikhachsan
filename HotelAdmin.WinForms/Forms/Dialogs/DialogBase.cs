using HotelAdmin.Controls;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Khung chung cho mọi hộp thoại: tiêu đề, vùng nội dung (Body) cuộn được, thanh nút phía dưới.
/// Dùng RunAsync để gọi API: tự khóa nút, hiện con trỏ chờ và báo lỗi thân thiện.
/// </summary>
public class DialogBase : Form
{
    private readonly FlowLayoutPanel footer = new();
    private readonly Label subtitle = new();

    protected DialogBase(string title, int width, int height, string? sub = null)
    {
        Text = title;
        Font = AppTheme.Body;
        BackColor = AppTheme.Surface;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(width, height);
        DoubleBuffered = true;

        var header = new Panel { Dock = DockStyle.Top, Height = sub is null ? 64 : 84, BackColor = AppTheme.Surface, Padding = new Padding(24, 18, 24, 0) };
        var titleLabel = new Label { Text = title, Font = AppTheme.CardTitle, ForeColor = AppTheme.Text, AutoSize = true, Location = new Point(24, 18) };
        header.Controls.Add(titleLabel);
        if (sub is not null)
        {
            subtitle.Text = sub;
            subtitle.Font = AppTheme.Small;
            subtitle.ForeColor = AppTheme.TextMuted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(25, 48);
            header.Controls.Add(subtitle);
        }

        footer.Dock = DockStyle.Bottom;
        footer.Height = 68;
        footer.FlowDirection = FlowDirection.RightToLeft;
        footer.Padding = new Padding(16, 14, 16, 14);
        footer.BackColor = AppTheme.Background;

        Body.Dock = DockStyle.Fill;
        Body.AutoScroll = true;
        Body.Padding = new Padding(24, 8, 24, 16);
        Body.BackColor = AppTheme.Surface;

        Controls.Add(Body);
        Controls.Add(footer);
        Controls.Add(header);
    }

    /// <summary>Vùng nội dung của hộp thoại.</summary>
    protected Panel Body { get; } = new();

    protected string Subtitle
    {
        get => subtitle.Text;
        set => subtitle.Text = value;
    }

    /// <summary>Thêm nút ở chân hộp thoại (thêm theo thứ tự từ phải sang trái).</summary>
    protected FlatButton AddButton(string text, bool primary, EventHandler onClick)
    {
        var button = new FlatButton
        {
            Text = text,
            Size = new Size(Math.Max(110, TextRenderer.MeasureText(text, AppTheme.BodyBold).Width + 36), 40),
            FillColor = primary ? AppTheme.Lime : AppTheme.Surface,
            HoverColor = primary ? AppTheme.LimeHover : AppTheme.Hover,
            BorderColor = primary ? Color.Transparent : AppTheme.Border,
            Margin = new Padding(8, 0, 0, 0)
        };
        button.Click += onClick;
        footer.Controls.Add(button);
        return button;
    }

    /// <summary>Nút Đóng/Hủy chuẩn.</summary>
    protected FlatButton AddCancelButton(string text = "Hủy")
        => AddButton(text, false, (_, _) => { DialogResult = DialogResult.Cancel; Close(); });

    /// <summary>Gọi API an toàn: khóa nút, báo lỗi; trả về true nếu thành công.</summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        foreach (Control c in footer.Controls) c.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex)
        {
            Ui.Error(this, ex.Message);
            return false;
        }
        finally
        {
            UseWaitCursor = false;
            if (!IsDisposed) foreach (Control c in footer.Controls) c.Enabled = true;
        }
    }

    protected void CloseOk()
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Tiện ích giao diện dùng chung: định dạng tiền/ngày, hộp thông báo, hỏi xác nhận, nhập nhanh.</summary>
public static class Ui
{
    public static string Money(decimal value) => value.ToString("#,0") + " đ";

    public static string DateText(DateTime value) => value.ToString("dd/MM/yyyy");

    public static string DateTimeText(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");

    public static void Error(IWin32Window? owner, string message)
        => MessageBox.Show(owner, message, "Không thực hiện được", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Info(IWin32Window? owner, string message, string title = "Thông báo")
        => MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(IWin32Window? owner, string message, string title = "Xác nhận")
        => MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>Hộp nhập một dòng/nhiều dòng; trả null nếu người dùng hủy.</summary>
    public static string? Prompt(IWin32Window? owner, string title, string label, string? initial = null, bool required = true, bool multiline = true)
    {
        using var dlg = new PromptDialog(title, label, initial, required, multiline);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
    }

    /// <summary>Hộp nhập số tiền; trả null nếu hủy.</summary>
    public static decimal? PromptMoney(IWin32Window? owner, string title, string label, decimal initial, decimal max)
    {
        using var dlg = new MoneyPromptDialog(title, label, initial, max);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
    }

    private sealed class PromptDialog : DialogBase
    {
        private readonly TextBox box;
        private readonly bool required;

        public PromptDialog(string title, string label, string? initial, bool required, bool multiline)
            : base(title, 460, multiline ? 300 : 230)
        {
            this.required = required;
            var grid = new FormGrid(1);
            box = grid.Add(label, Inputs.Text(initial, multiline, 90));
            Body.Controls.Add(grid);
            AddButton("Đồng ý", true, (_, _) => Accept());
            AddCancelButton();
        }

        public string Value => box.Text.Trim();

        private void Accept()
        {
            if (required && Value.Length == 0)
            {
                Ui.Error(this, "Vui lòng nhập nội dung.");
                return;
            }
            CloseOk();
        }
    }

    private sealed class MoneyPromptDialog : DialogBase
    {
        private readonly NumericUpDown number;

        public MoneyPromptDialog(string title, string label, decimal initial, decimal max) : base(title, 420, 230)
        {
            var grid = new FormGrid(1);
            number = grid.Add(label, Inputs.Number(initial, 0, Math.Max(0, max), 0, 50000));
            Body.Controls.Add(grid);
            AddButton("Đồng ý", true, (_, _) => CloseOk());
            AddCancelButton();
        }

        public decimal Value => number.Value;
    }
}
