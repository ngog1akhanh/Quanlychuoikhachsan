using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Trang Nhận / trả phòng (mục 8): chọn ngày → 3 khối Khách đến, Khách đi, Đang lưu trú.
/// Chọn dòng rồi bấm Nhận phòng / Trả phòng / Xem; bấm đúp để mở chi tiết booking.
/// </summary>
public sealed class FrontDeskPage : PageBase
{
    private readonly DateTimePicker date = Inputs.Date(DateTime.Today);
    private readonly StatCard arrivalsCard = new("Khách đến", Glyph.ArrowIn, highlight: true);
    private readonly StatCard departuresCard = new("Khách đi", Glyph.ArrowOut);
    private readonly StatCard inHouseCard = new("Đang lưu trú", Glyph.Rooms);
    private readonly ModernGrid arrivals = new() { Dock = DockStyle.Fill };
    private readonly ModernGrid departures = new() { Dock = DockStyle.Fill };
    private readonly ModernGrid inHouse = new() { Dock = DockStyle.Fill };
    private FrontDeskDay? day;

    public FrontDeskPage(ApiClient api, UserSession session) : base(api, session)
    {
        Padding = new Padding(24, 0, 24, 16);

        // ---- Thanh chọn ngày ----
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false, BackColor = AppTheme.Background, Padding = new Padding(0, 6, 0, 6) };
        date.Width = 140;
        date.CloseUp += async (_, _) => await ReloadSafeAsync();
        var today = new FlatButton { Text = "Hôm nay", Size = new Size(100, 34), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        today.Click += async (_, _) => { date.Value = DateTime.Today; await ReloadSafeAsync(); };
        bar.Controls.AddRange([Inputs.Caption("Ngày", AppTheme.SmallBold, AppTheme.TextSoft), date, today]);
        foreach (Control c in bar.Controls) c.Margin = new Padding(0, c is Label ? 10 : 2, 10, 0);

        // ---- Bố cục: hàng KPI + 3 khối ----
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = AppTheme.Background };
        for (var i = 0; i < 3; i++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var card in new[] { arrivalsCard, departuresCard, inHouseCard })
        {
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 12, 12);
        }
        layout.Controls.Add(arrivalsCard, 0, 0);
        layout.Controls.Add(departuresCard, 1, 0);
        layout.Controls.Add(inHouseCard, 2, 0);

        layout.Controls.Add(Block("Khách đến", arrivals, ("Nhận phòng", "checkin", "BOOKING_WRITE")), 0, 1);
        layout.Controls.Add(Block("Khách đi", departures, ("Trả phòng", "checkout", "BOOKING_WRITE")), 1, 1);
        layout.Controls.Add(Block("Đang lưu trú", inHouse, ("Gia hạn", "dates", "BOOKING_WRITE")), 2, 1);

        Controls.Add(layout);
        Controls.Add(bar);
    }

    public override string Title => "Nhận / trả phòng";

    private Control Block(string title, ModernGrid grid, (string Text, string Action, string Permission) primary)
    {
        var (card, body, header) = CreateCard(title, 0);
        card.Margin = new Padding(0, 0, 12, 0);

        grid.AutoGenerateColumns = false;
        grid.AddText("code", "Booking", 80);
        grid.AddText("guest", "Khách", 100);
        grid.AddText("rooms", "Phòng", 50);
        grid.AddText("time", "Giờ", 50);
        grid.AddStatus("status", "Trạng thái", v => StatusStyles.ForBooking(v as string), 90);
        grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await RunAsync(grid, "view"); };

        var view = new FlatButton { Text = "Xem", Size = new Size(64, 32), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border, Margin = new Padding(6, 4, 0, 0) };
        view.Click += async (_, _) => await RunAsync(grid, "view");
        header.Controls.Add(view);
        if (Session.HasPermission(primary.Permission))
        {
            var main = new FlatButton { Text = primary.Text, Size = new Size(TextRenderer.MeasureText(primary.Text, AppTheme.BodyBold).Width + 28, 32), Margin = new Padding(6, 4, 0, 0) };
            main.Click += async (_, _) => await RunAsync(grid, primary.Action);
            header.Controls.Add(main);
        }
        body.Controls.Add(grid);
        return card;
    }

    public override async Task LoadDataAsync()
    {
        day = await Api.GetFrontDeskDayAsync(DateOnly.FromDateTime(date.Value));
        Fill(arrivals, day.Arrivals, b => b.CheckIn);
        Fill(departures, day.Departures, b => b.CheckOut);
        Fill(inHouse, day.InHouse, b => b.CheckOut);

        var arrived = day.Arrivals.Count(b => b.Status == "DANG_O");
        var waitingDeposit = day.Arrivals.Count(b => b.Status is "HOLD" or "CHO_COC");
        arrivalsCard.Value = day.Arrivals.Count.ToString();
        arrivalsCard.Note = $"{arrived} đã nhận · {waitingDeposit} chưa cọc";
        var left = day.Departures.Count(b => b.Status == "DA_TRA");
        departuresCard.Value = day.Departures.Count.ToString();
        departuresCard.Note = $"{left} đã trả · {day.Departures.Count - left} chưa trả";
        inHouseCard.Value = day.InHouse.Count.ToString();
        inHouseCard.Note = $"{day.InHouse.Sum(b => b.Adults + b.Children)} khách đang ở";
    }

    private static void Fill(ModernGrid grid, List<FrontDeskBookingItem> items, Func<FrontDeskBookingItem, DateTime> time)
    {
        grid.Tag = items;
        grid.Rows.Clear();
        foreach (var b in items)
            grid.Rows.Add(b.BookingCode, b.CustomerName, b.Rooms, time(b).ToString(time(b).Date == DateTime.Today ? "HH:mm" : "dd/MM HH:mm"), b.Status);
    }

    private async Task RunAsync(ModernGrid grid, string action)
    {
        if (grid.Tag is not List<FrontDeskBookingItem> items || grid.CurrentRow is null || grid.CurrentRow.Index >= items.Count)
        {
            Ui.Error(FindForm(), "Chọn một booking trong danh sách.");
            return;
        }
        var item = items[grid.CurrentRow.Index];
        try
        {
            if (action == "view")
            {
                using var dlg = new DlgBookingDetail(Api, Session, item.Id);
                dlg.ShowDialog(FindForm());
                if (dlg.Changed) await ReloadSafeAsync();
                return;
            }
            var detail = await Api.GetBookingDetailAsync(item.Id);
            if (await BookingActions.RunAsync(action, FindForm()!, Api, detail)) await ReloadSafeAsync();
        }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); }
    }

    private async Task ReloadSafeAsync()
    {
        try { await LoadDataAsync(); }
        catch (ApiException ex) { Ui.Error(FindForm(), ex.Message); }
    }
}
