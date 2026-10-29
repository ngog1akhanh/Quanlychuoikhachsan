using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Controls;

/// <summary>
/// Khung chi tiết một booking (giống mẫu "Guest Profile"): thông tin khách, ngày ở, các nút thao tác theo trạng thái,
/// phòng, giá từng đêm, khách lưu trú, tiền cọc, hóa đơn, yêu cầu hủy, lịch sử trạng thái.
/// Nút bấm không tự gọi API mà phát sự kiện ActionRequested (deposit, dates, room, cancel, noshow, checkin, checkout, refresh)
/// để trang/hộp thoại chứa nó xử lý qua BookingActions.
/// </summary>
public sealed class BookingDetailView : Panel
{
    private readonly FlowLayoutPanel flow = new();
    private readonly UserSession session;

    public BookingDetailView(UserSession session)
    {
        this.session = session;
        BackColor = AppTheme.Surface;
        flow.Dock = DockStyle.Fill;
        flow.FlowDirection = FlowDirection.TopDown;
        flow.WrapContents = false;
        flow.AutoScroll = true;
        flow.BackColor = AppTheme.Surface;
        flow.Padding = new Padding(4, 4, 4, 12);
        flow.Resize += (_, _) => FitWidths();
        Controls.Add(flow);
        ShowMessage("Chọn một booking để xem chi tiết.");
    }

    /// <summary>Phát ra khi người dùng bấm một nút thao tác.</summary>
    public event EventHandler<string>? ActionRequested;

    public FrontDeskBookingDetail? Detail { get; private set; }

    public void ShowMessage(string text)
    {
        Detail = null;
        flow.Controls.Clear();
        flow.Controls.Add(new Label
        {
            Text = text,
            AutoSize = false,
            Height = 60,
            Font = AppTheme.Body,
            ForeColor = AppTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        });
        FitWidths();
    }

    public void SetDetail(FrontDeskBookingDetail d)
    {
        Detail = d;
        var s = d.Summary;
        flow.SuspendLayout();
        flow.Controls.Clear();

        // ---- Đầu: mã, trạng thái, khách, ngày ----
        var head = new Panel { Height = 112, BackColor = AppTheme.Surface, Margin = new Padding(0, 0, 0, 6) };
        head.Controls.Add(new Label { Text = s.BookingCode, Font = AppTheme.CardTitle, ForeColor = AppTheme.Text, AutoSize = true, Location = new Point(0, 2) });
        var pill = new PillLabel { Location = new Point(TextRenderer.MeasureText(s.BookingCode, AppTheme.CardTitle).Width + 10, 4) };
        pill.ApplyStatus(StatusStyles.ForBooking(s.Status));
        head.Controls.Add(pill);
        head.Controls.Add(Line($"{d.Customer.FullName}  ·  {d.Customer.Phone ?? "chưa có SĐT"}  ·  {ChannelText(s.Channel)}", AppTheme.BodyBold, AppTheme.Text, 34));
        head.Controls.Add(Line($"Nhận {s.CheckIn:dd/MM/yyyy HH:mm}  →  Trả {s.CheckOut:dd/MM/yyyy HH:mm}  ·  {s.Nights} đêm  ·  {s.Adults} người lớn, {s.Children} trẻ em", AppTheme.Body, AppTheme.TextSoft, 58));
        head.Controls.Add(Line($"Tổng tiền phòng {Money(s.Total)}  ·  Đã cọc {Money(s.DepositPaid)}", AppTheme.Body, AppTheme.TextSoft, 82));
        head.Resize += (_, _) =>
        {
            foreach (Control c in head.Controls)
                if (c is Label { AutoEllipsis: true } l) l.Width = Math.Max(100, head.Width - l.Left);
        };
        flow.Controls.Add(head);

        if (d.HoldExpiresAt is { } hold && s.Status is "HOLD" or "CHO_COC")
            flow.Controls.Add(Banner($"Đang giữ chỗ chờ cọc đến {hold.ToLocalTime():dd/MM HH:mm}. Quá hạn, phòng có thể bị người khác đặt.", AppTheme.WarningBg, AppTheme.WarningFg));
        if (s.Status == "CHO_DUYET_HUY")
            flow.Controls.Add(Banner("Booking đang chờ quản lý duyệt yêu cầu hủy.", AppTheme.WarningBg, AppTheme.WarningFg));
        if (!string.IsNullOrWhiteSpace(d.SpecialRequest))
            flow.Controls.Add(Banner($"Yêu cầu đặc biệt: {d.SpecialRequest}", AppTheme.MintSoft, AppTheme.Forest));

        // ---- Nút thao tác theo trạng thái + quyền ----
        flow.Controls.Add(BuildActions(d));

        // ---- Phòng ----
        flow.Controls.Add(SectionTitle($"Phòng ({d.Rooms.Count})"));
        var rooms = SmallGrid();
        rooms.AddText("room", "Phòng", 50);
        rooms.AddText("type", "Loại", 80);
        rooms.AddStatus("rstatus", "Tình trạng phòng", v => StatusStyles.ForRoom(v as string), 90);
        rooms.AddText("amount", "Tiền phòng", 80, "#,0 đ", DataGridViewContentAlignment.MiddleRight);
        foreach (var r in d.Rooms) rooms.Rows.Add(r.RoomNumber, r.RoomTypeName, r.RoomStatus, r.Amount);
        flow.Controls.Add(Fit(rooms));

        // ---- Giá từng đêm ----
        flow.Controls.Add(SectionTitle("Giá từng đêm (đã chốt)"));
        var nights = SmallGrid();
        nights.AddText("date", "Đêm", 70);
        nights.AddText("room", "Phòng", 50);
        nights.AddText("base", "Giá cơ bản", 80, "#,0", DataGridViewContentAlignment.MiddleRight);
        nights.AddText("adj", "Điều chỉnh", 70, "#,0", DataGridViewContentAlignment.MiddleRight);
        nights.AddText("sur", "Phụ thu", 60, "#,0", DataGridViewContentAlignment.MiddleRight);
        nights.AddText("final", "Thành tiền", 80, "#,0", DataGridViewContentAlignment.MiddleRight);
        foreach (var n in d.Nights) nights.Rows.Add(n.StayDate.ToString("dd/MM/yyyy"), n.RoomNumber, n.BasePrice, n.Adjustment, n.Surcharge, n.FinalPrice);
        flow.Controls.Add(Fit(nights));

        // ---- Khách lưu trú ----
        flow.Controls.Add(SectionTitle($"Khách lưu trú ({d.Guests.Count})"));
        if (d.Guests.Count == 0)
            flow.Controls.Add(Hint("Chưa khai báo. Khai báo khi làm thủ tục nhận phòng."));
        else
        {
            var roomOf = d.Rooms.ToDictionary(r => r.DetailId, r => r.RoomNumber);
            var guests = SmallGrid();
            guests.AddText("room", "Phòng", 40);
            guests.AddText("name", "Họ tên", 110);
            guests.AddText("doc", "Giấy tờ", 110);
            guests.AddText("nat", "Quốc tịch", 60);
            foreach (var g in d.Guests)
                guests.Rows.Add(roomOf.GetValueOrDefault(g.DetailId, "?"), g.FullName + (g.IsRepresentative ? " (đại diện)" : ""),
                    $"{g.DocumentType} {g.DocumentNumber}".Trim(), g.Nationality ?? "");
            flow.Controls.Add(Fit(guests));
        }

        // ---- Tiền cọc ----
        flow.Controls.Add(SectionTitle("Tiền cọc"));
        if (d.Deposits.Count == 0) flow.Controls.Add(Hint("Chưa có phiếu cọc."));
        else
        {
            var deposits = SmallGrid();
            deposits.AddText("code", "Mã phiếu", 90);
            deposits.AddText("time", "Thời gian", 90);
            deposits.AddText("method", "Phương thức", 80);
            deposits.AddText("amount", "Số tiền", 70, "#,0 đ", DataGridViewContentAlignment.MiddleRight);
            foreach (var x in d.Deposits) deposits.Rows.Add(x.Code, x.PaidAt.ToString("dd/MM/yyyy HH:mm"), x.MethodName, x.Amount);
            flow.Controls.Add(Fit(deposits));
        }

        // ---- Hóa đơn ----
        flow.Controls.Add(SectionTitle("Hóa đơn"));
        if (d.Invoices.Count == 0) flow.Controls.Add(Hint("Hóa đơn được lập khi khách nhận/trả phòng."));
        else
        {
            var invoices = SmallGrid();
            invoices.AddText("code", "Mã", 90);
            invoices.AddText("total", "Tổng", 70, "#,0", DataGridViewContentAlignment.MiddleRight);
            invoices.AddText("dep", "Cọc trừ", 60, "#,0", DataGridViewContentAlignment.MiddleRight);
            invoices.AddText("paid", "Đã thu", 60, "#,0", DataGridViewContentAlignment.MiddleRight);
            invoices.AddText("due", "Còn nợ", 60, "#,0", DataGridViewContentAlignment.MiddleRight);
            invoices.AddText("st", "Trạng thái", 70);
            foreach (var x in d.Invoices) invoices.Rows.Add(x.Code, x.Total, x.DepositApplied, x.Paid, x.Due, InvoiceStatusText(x.Status));
            flow.Controls.Add(Fit(invoices));
        }

        // ---- Yêu cầu hủy ----
        if (d.Cancellations.Count > 0)
        {
            flow.Controls.Add(SectionTitle("Yêu cầu hủy"));
            var cancels = SmallGrid();
            cancels.AddText("time", "Thời gian", 70);
            cancels.AddText("reason", "Lý do", 130);
            cancels.AddText("refund", "Hoàn (dự kiến/duyệt)", 90);
            cancels.AddText("st", "Trạng thái", 60);
            foreach (var c in d.Cancellations)
                cancels.Rows.Add(c.RequestedAt.ToString("dd/MM HH:mm"), c.Reason, $"{c.ExpectedRefund:#,0} / {c.ApprovedRefund:#,0}", CancelStatusText(c.Status));
            flow.Controls.Add(Fit(cancels));
        }

        // ---- Lịch sử ----
        flow.Controls.Add(SectionTitle("Lịch sử thay đổi"));
        if (d.History.Count == 0) flow.Controls.Add(Hint("Chưa có lịch sử (booking tạo trước Giai đoạn 1 hoặc từ website)."));
        else
        {
            var history = SmallGrid();
            history.AddText("time", "Thời gian", 70);
            history.AddText("change", "Trạng thái", 90);
            history.AddText("by", "Người làm", 70);
            history.AddText("reason", "Nội dung", 160);
            foreach (var h in d.History.OrderByDescending(x => x.At))
            {
                var change = h.From is null || h.From == h.To
                    ? StatusStyles.ForBooking(h.To).Label
                    : $"{StatusStyles.ForBooking(h.From).Label} → {StatusStyles.ForBooking(h.To).Label}";
                history.Rows.Add(h.At.ToString("dd/MM HH:mm"), change, h.By ?? "-", h.Reason ?? "");
            }
            flow.Controls.Add(Fit(history));
        }

        flow.ResumeLayout();
        FitWidths();
    }

    // =====================================================================
    private Control BuildActions(FrontDeskBookingDetail d)
    {
        var s = d.Summary;
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = AppTheme.Surface, Margin = new Padding(0, 4, 0, 4) };
        var today = DateOnly.FromDateTime(DateTime.Today);
        var arrived = DateOnly.FromDateTime(s.CheckIn) <= today;
        var canWrite = session.HasPermission("BOOKING_WRITE");
        var canDesk = canWrite;
        var canDeposit = session.HasPermission("BILLING_PAYMENT_WRITE");
        var canCheckOut = canDesk && session.HasPermission("BILLING_READ") && session.HasPermission("BILLING_WRITE") && session.HasPermission("BILLING_PAYMENT_WRITE");

        void Add(string action, string text, string glyph, bool primary = false)
        {
            var b = new FlatButton
            {
                Text = text,
                IconGlyph = glyph,
                Size = new Size(TextRenderer.MeasureText(text, AppTheme.BodyBold).Width + 46, 36),
                FillColor = primary ? AppTheme.Lime : AppTheme.Surface,
                HoverColor = primary ? AppTheme.LimeHover : AppTheme.Hover,
                BorderColor = primary ? Color.Transparent : AppTheme.Border,
                Margin = new Padding(0, 0, 8, 8)
            };
            b.Click += (_, _) => ActionRequested?.Invoke(this, action);
            panel.Controls.Add(b);
        }

        switch (s.Status)
        {
            case "HOLD" or "CHO_COC":
                if (canDeposit) Add("deposit", "Xác nhận cọc", Glyph.Money, true);
                if (canWrite) Add("dates", "Đổi ngày", Glyph.Calendar);
                if (canWrite) Add("room", "Đổi phòng", Glyph.Switch);
                if (canWrite) Add("cancel", "Hủy", Glyph.Warning);
                break;
            case "BOOKED":
                if (canDesk && arrived) Add("checkin", "Nhận phòng", Glyph.ArrowIn, true);
                if (canWrite) Add("dates", "Đổi ngày", Glyph.Calendar);
                if (canWrite) Add("room", "Đổi phòng", Glyph.Switch);
                if (canWrite) Add("cancel", "Yêu cầu hủy", Glyph.Warning);
                if (canWrite && arrived) Add("noshow", "Không đến", Glyph.Clock);
                break;
            case "DANG_O":
                if (canCheckOut) Add("checkout", "Trả phòng", Glyph.ArrowOut, true);
                if (canWrite) Add("dates", "Gia hạn / rút ngắn", Glyph.Calendar);
                if (canWrite) Add("room", "Đổi phòng", Glyph.Switch);
                break;
        }
        Add("refresh", "Làm mới", Glyph.Refresh);
        return panel;
    }

    private void FitWidths()
    {
        var w = Math.Max(200, flow.ClientSize.Width - flow.Padding.Horizontal - 6);
        foreach (Control c in flow.Controls)
        {
            if (c is FlowLayoutPanel f) { f.MaximumSize = new Size(w, 0); f.Width = w; continue; }
            c.Width = w;
        }
    }

    private static ModernGrid SmallGrid()
    {
        var grid = new ModernGrid
        {
            ScrollBars = ScrollBars.None,
            ColumnHeadersHeight = 32,
            Margin = new Padding(0, 0, 0, 8)
        };
        grid.RowTemplate.Height = 32;
        grid.DefaultCellStyle.Font = AppTheme.Small;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 2, 0);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        return grid;
    }

    /// <summary>Đặt chiều cao lưới vừa khít số dòng (không thanh cuộn trong lưới).</summary>
    private static ModernGrid Fit(ModernGrid grid)
    {
        grid.Height = grid.ColumnHeadersHeight + Math.Max(1, grid.Rows.Count) * grid.RowTemplate.Height + 2;
        return grid;
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        Font = AppTheme.BodyBold,
        ForeColor = AppTheme.Text,
        AutoSize = false,
        Height = 30,
        TextAlign = ContentAlignment.BottomLeft,
        Margin = new Padding(0, 8, 0, 4)
    };

    private static Label Hint(string text) => new()
    {
        Text = text,
        Font = AppTheme.Small,
        ForeColor = AppTheme.TextMuted,
        AutoSize = false,
        Height = 24,
        Margin = new Padding(0, 0, 0, 6)
    };

    private static Label Banner(string text, Color back, Color fore) => new()
    {
        Text = text,
        Font = AppTheme.Small,
        ForeColor = fore,
        BackColor = back,
        AutoSize = false,
        Height = 34,
        Padding = new Padding(10, 0, 10, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 0, 6)
    };

    private static Label Line(string text, Font font, Color color, int top) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        AutoSize = false,
        AutoEllipsis = true,
        Location = new Point(0, top),
        Size = new Size(400, 22)
    };

    private static string Money(decimal v) => v.ToString("#,0") + " đ";

    public static string ChannelText(string channel) => channel switch
    {
        "WEB" => "Website",
        "QUAY" => "Tại quầy",
        "DIEN_THOAI" => "Điện thoại",
        _ => channel
    };

    public static string InvoiceStatusText(string status) => status switch
    {
        "NHAP" => "Nháp",
        "DA_PHAT_HANH" => "Đã phát hành",
        "TT_MOT_PHAN" => "Thanh toán 1 phần",
        "DA_THANH_TOAN" => "Đã thanh toán",
        "DA_HUY" => "Đã hủy",
        _ => status
    };

    public static string CancelStatusText(string status) => status switch
    {
        "CHO_DUYET" => "Chờ duyệt",
        "DA_DUYET" => "Đã duyệt",
        "TU_CHOI" => "Từ chối",
        _ => status
    };
}
