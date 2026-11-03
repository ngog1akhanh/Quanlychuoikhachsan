using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Điều phối các thao tác trên một booking (dùng chung cho trang Đặt phòng, Lễ tân, Sơ đồ phòng).
/// Trả về true nếu dữ liệu đã thay đổi (để màn hình gọi tải lại).
/// </summary>
public static class BookingActions
{
    public static async Task<bool> RunAsync(string action, IWin32Window owner, ApiClient api, FrontDeskBookingDetail d)
    {
        switch (action)
        {
            case "deposit":
                using (var dlg = new DlgDeposit(api, d)) return dlg.ShowDialog(owner) == DialogResult.OK;
            case "dates":
                using (var dlg = new DlgChangeDates(api, d)) return dlg.ShowDialog(owner) == DialogResult.OK;
            case "room":
                using (var dlg = new DlgChangeRoom(api, d)) return dlg.ShowDialog(owner) == DialogResult.OK;
            case "checkin":
                using (var dlg = new DlgCheckIn(api, d)) return dlg.ShowDialog(owner) == DialogResult.OK;
            case "checkout":
                using (var dlg = new DlgCheckOut(api, d)) return dlg.ShowDialog(owner) == DialogResult.OK;
            case "cancel":
            {
                var hold = d.Summary.Status is "HOLD" or "CHO_COC";
                var question = hold
                    ? "Booking chưa cọc sẽ bị hủy ngay và phòng được giải phóng. Lý do hủy:"
                    : "Booking đã cọc: hệ thống tạo yêu cầu hủy để quản lý duyệt. Lý do hủy:";
                var reason = Ui.Prompt(owner, $"Hủy booking {d.Summary.BookingCode}", question);
                if (reason is null) return false;
                try
                {
                    var result = await api.CancelBookingAsync(d.Summary.Id, reason);
                    Ui.Info(owner, result.Message);
                    return true;
                }
                catch (ApiException ex) { Ui.Error(owner, ex.Message); return false; }
            }
            case "noshow":
            {
                if (!Ui.Confirm(owner, $"Đánh dấu booking {d.Summary.BookingCode} là khách không đến?\nCác đêm từ hôm nay sẽ được giải phóng; tiền cọc xử lý theo chính sách.")) return false;
                var note = Ui.Prompt(owner, "Ghi chú", "Ghi chú (không bắt buộc):", null, required: false);
                try
                {
                    await api.MarkNoShowAsync(d.Summary.Id, note);
                    return true;
                }
                catch (ApiException ex) { Ui.Error(owner, ex.Message); return false; }
            }
            default:
                return false;
        }
    }
}

/// <summary>Hộp thoại xem chi tiết booking và thao tác (mở từ lịch phòng, sơ đồ phòng, lễ tân).</summary>
public sealed class DlgBookingDetail : DialogBase
{
    private readonly ApiClient api;
    private readonly long bookingId;
    private readonly BookingDetailView view;

    public DlgBookingDetail(ApiClient api, UserSession session, long bookingId) : base("Chi tiết booking", 860, 780)
    {
        this.api = api;
        this.bookingId = bookingId;
        view = new BookingDetailView(session) { Dock = DockStyle.Fill };
        view.ActionRequested += async (_, action) => await OnActionAsync(action);
        Body.AutoScroll = false;
        Body.Controls.Add(view);
        AddButton("Đóng", false, (_, _) => Close());
        Shown += async (_, _) => await ReloadAsync();
    }

    /// <summary>true nếu đã có thao tác làm thay đổi dữ liệu.</summary>
    public bool Changed { get; private set; }

    private async Task ReloadAsync()
    {
        await RunAsync(async () => view.SetDetail(await api.GetBookingDetailAsync(bookingId)));
    }

    private async Task OnActionAsync(string action)
    {
        if (action == "refresh" || view.Detail is null) { await ReloadAsync(); return; }
        if (await BookingActions.RunAsync(action, this, api, view.Detail))
        {
            Changed = true;
            await ReloadAsync();
        }
    }
}

/// <summary>Thu tiền cọc tại quầy để xác nhận booking đang giữ chỗ (mục 7.3).</summary>
public sealed class DlgDeposit : DialogBase
{
    private readonly ApiClient api;
    private readonly FrontDeskBookingDetail booking;
    private readonly ComboBox method = Inputs.Combo([]);
    private readonly NumericUpDown amount;
    private readonly TextBox note = Inputs.Text();
    private readonly string depositReference = Guid.NewGuid().ToString("N");

    public DlgDeposit(ApiClient api, FrontDeskBookingDetail booking)
        : base("Xác nhận tiền cọc", 500, 360, $"{booking.Summary.BookingCode} · Tổng tiền phòng {Ui.Money(booking.Summary.Total)}")
    {
        this.api = api;
        this.booking = booking;
        var remaining = Math.Max(0, booking.Summary.Total - booking.Summary.DepositPaid);
        amount = Inputs.Number(Math.Min(remaining, SuggestDeposit(booking.Summary.Total)), 0, remaining, 0, 50000);

        var grid = new FormGrid(2);
        grid.Add("Phương thức", method);
        grid.Add("Số tiền cọc (gợi ý 30%)", amount);
        grid.Add("Ghi chú", note, 2);
        Body.Controls.Add(grid);

        AddButton("Xác nhận cọc", true, async (_, _) => await SaveAsync());
        AddCancelButton();
        Shown += async (_, _) => await RunAsync(async () => FillMethods(method, await api.GetPaymentMethodsAsync()));
    }

    public static decimal SuggestDeposit(decimal total) => Math.Ceiling(total * 0.3m);

    public static void FillMethods(ComboBox combo, List<PaymentMethodItem> methods)
    {
        combo.Items.Clear();
        foreach (var m in methods) combo.Items.Add(new Choice<long>(m.Name, m.Id));
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private async Task SaveAsync()
    {
        if (method.SelectedItem is not Choice<long> m) { Ui.Error(this, "Chọn phương thức thanh toán."); return; }
        if (amount.Value <= 0) { Ui.Error(this, "Số tiền cọc phải lớn hơn 0."); return; }
        var ok = await RunAsync(() => api.ConfirmDepositAsync(booking.Summary.Id, new DeskDepositRequest(m.Value, amount.Value, note.Text, depositReference)));
        if (ok) CloseOk();
    }
}

/// <summary>Đổi ngày lưu trú / gia hạn (mục 7.4). Khách đang ở chỉ được đổi ngày trả phòng.</summary>
public sealed class DlgChangeDates : DialogBase
{
    private readonly ApiClient api;
    private readonly FrontDeskBookingDetail booking;
    private readonly DateTimePicker checkIn;
    private readonly DateTimePicker checkOut;
    private readonly TextBox reason = Inputs.Text(null, true, 70);
    private readonly Label nights = Inputs.Caption("", AppTheme.BodyBold);

    public DlgChangeDates(ApiClient api, FrontDeskBookingDetail booking)
        : base(booking.Summary.Status == "DANG_O" ? "Gia hạn / rút ngắn lưu trú" : "Đổi ngày lưu trú", 520, 420,
            $"{booking.Summary.BookingCode} · hiện tại {booking.Summary.CheckIn:dd/MM HH:mm} → {booking.Summary.CheckOut:dd/MM HH:mm}")
    {
        this.api = api;
        this.booking = booking;
        checkIn = Inputs.Date(booking.Summary.CheckIn, true);
        checkOut = Inputs.Date(booking.Summary.CheckOut, true);
        checkIn.Enabled = booking.Summary.Status != "DANG_O";

        var grid = new FormGrid(2);
        grid.Add("Nhận phòng", checkIn);
        grid.Add("Trả phòng", checkOut);
        grid.AddFull(nights);
        grid.Add("Lý do", reason, 2);
        Body.Controls.Add(grid);

        checkIn.ValueChanged += (_, _) => UpdateNights();
        checkOut.ValueChanged += (_, _) => UpdateNights();
        UpdateNights();

        AddButton("Lưu thay đổi", true, async (_, _) => await SaveAsync());
        AddCancelButton();
    }

    private void UpdateNights()
    {
        var n = (checkOut.Value.Date - checkIn.Value.Date).Days;
        nights.Text = n > 0 ? $"{n} đêm. Giá các đêm giữ lại không đổi; đêm mới tính theo bảng giá hiện hành." : "Ngày trả phải sau ngày nhận.";
        nights.ForeColor = n > 0 ? AppTheme.TextSoft : AppTheme.DangerFg;
    }

    private async Task SaveAsync()
    {
        if (checkOut.Value.Date <= checkIn.Value.Date) { Ui.Error(this, "Ngày trả phòng phải sau ngày nhận phòng."); return; }
        ChangeResult? result = null;
        var ok = await RunAsync(async () =>
            result = await api.ChangeDatesAsync(booking.Summary.Id, new ChangeDatesRequest(checkIn.Value, checkOut.Value, reason.Text)));
        if (!ok || result is null) return;
        Ui.Info(this, result.Message);
        CloseOk();
    }
}

/// <summary>Đổi phòng cho khách (mục 7.5): chọn phòng trong booking và phòng mới còn trống cho các đêm còn lại.</summary>
public sealed class DlgChangeRoom : DialogBase
{
    private readonly ApiClient api;
    private readonly FrontDeskBookingDetail booking;
    private readonly ComboBox current;
    private readonly ModernGrid rooms = new() { Height = 260 };
    private readonly TextBox reason = Inputs.Text(null, true, 60);
    private List<AvailableRoom> available = [];

    public DlgChangeRoom(ApiClient api, FrontDeskBookingDetail booking)
        : base("Đổi phòng", 640, 600, $"{booking.Summary.BookingCode} · {booking.Customer.FullName}")
    {
        this.api = api;
        this.booking = booking;
        current = Inputs.Combo(booking.Rooms.Select(r => (object)new Choice<BookingRoomLine>($"Phòng {r.RoomNumber} ({r.RoomTypeName})", r)));

        rooms.AddText("RoomNumber", "Phòng", 50);
        rooms.AddText("Floor", "Tầng", 40);
        rooms.AddText("RoomTypeName", "Loại phòng", 90);
        rooms.AddText("Capacity", "Sức chứa", 60);
        rooms.AddText("Price", "Giá/đêm", 70, "#,0", DataGridViewContentAlignment.MiddleRight);
        rooms.AddStatus("Status", "Tình trạng", v => StatusStyles.ForRoom(v as string), 80);

        var grid = new FormGrid(1);
        grid.Add("Phòng cần đổi", current);
        grid.AddFull(Inputs.Caption(booking.Summary.Status == "DANG_O"
            ? "Khách đang ở: phòng mới phải Sẵn sàng; phòng cũ chuyển sang Cần dọn."
            : "Chỉ hiển thị phòng còn trống cho toàn bộ các đêm của booking.", AppTheme.Small, AppTheme.TextMuted));
        grid.Add("Phòng mới còn trống", rooms);
        grid.Add("Lý do", reason);
        Body.Controls.Add(grid);

        AddButton("Đổi phòng", true, async (_, _) => await SaveAsync());
        AddCancelButton();
        Shown += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var s = booking.Summary;
        var fromDate = s.Status == "DANG_O" ? DateOnly.FromDateTime(DateTime.Today) : DateOnly.FromDateTime(s.CheckIn);
        var toDate = DateOnly.FromDateTime(s.CheckOut);
        if (toDate <= fromDate) toDate = fromDate.AddDays(1);
        var mine = booking.Rooms.Select(r => r.RoomId).ToHashSet();
        await RunAsync(async () =>
        {
            available = (await api.GetAvailableRoomsAsync(fromDate, toDate, s.Id)).Where(r => !mine.Contains(r.Id)).ToList();
            rooms.Rows.Clear();
            foreach (var r in available)
                rooms.Rows.Add(r.RoomNumber, r.Floor ?? "", r.RoomTypeName, $"{r.MaxAdults} NL + {r.MaxChildren} TE", r.BaseNightlyPrice + r.NightSurcharge, r.Status);
        });
    }

    private async Task SaveAsync()
    {
        if (current.SelectedItem is not Choice<BookingRoomLine> line) { Ui.Error(this, "Chọn phòng cần đổi."); return; }
        if (rooms.CurrentRow is null || rooms.CurrentRow.Index >= available.Count) { Ui.Error(this, "Chọn phòng mới."); return; }
        var target = available[rooms.CurrentRow.Index];
        ChangeResult? result = null;
        var ok = await RunAsync(async () =>
            result = await api.ChangeRoomAsync(booking.Summary.Id, new ChangeRoomRequest(line.Value.DetailId, target.Id, reason.Text)));
        if (!ok || result is null) return;
        Ui.Info(this, result.Message);
        CloseOk();
    }
}

/// <summary>Nhận phòng (mục 8.2) kèm khai báo khách lưu trú (mục 8.3).</summary>
public sealed class DlgCheckIn : DialogBase
{
    private static readonly string[] Genders = ["", "Nam", "Nữ", "Khác"];
    private static readonly string[] DocTypes = ["CCCD", "Hộ chiếu", "GPLX", "Khác"];

    private readonly ApiClient api;
    private readonly FrontDeskBookingDetail booking;
    private readonly DataGridView guests = new();


    public DlgCheckIn(ApiClient api, FrontDeskBookingDetail booking)
        : base("Nhận phòng", 900, 620, $"{booking.Summary.BookingCode} · {booking.Customer.FullName} · {booking.Summary.Nights} đêm")
    {
        this.api = api;
        this.booking = booking;

        var notReady = booking.Rooms.Where(r => r.RoomStatus != "SAN_SANG").ToList();
        var roomInfo = string.Join("   ", booking.Rooms.Select(r => $"Phòng {r.RoomNumber}: {StatusStyles.ForRoom(r.RoomStatus).Label}"));
        var grid = new FormGrid(1);
        grid.AddFull(Inputs.Caption(roomInfo, AppTheme.BodyBold));
        if (notReady.Count > 0)
        {
            grid.AddFull(Inputs.Caption($"Cảnh báo: phòng {string.Join(", ", notReady.Select(r => r.RoomNumber))} chưa sẵn sàng. Nên đợi buồng phòng hoặc đổi phòng.",
                AppTheme.Small, AppTheme.DangerFg));

        }
        grid.AddFull(Inputs.Caption("Khai báo người ở thực tế (dòng cuối để thêm khách). Ngày sinh nhập dạng dd/MM/yyyy.", AppTheme.Small, AppTheme.TextMuted));
        BuildGuestGrid();
        grid.AddFull(guests);
        Body.Controls.Add(grid);

        AddButton("Nhận phòng", true, async (_, _) => await SaveAsync());
        AddCancelButton();
    }

    private void BuildGuestGrid()
    {
        guests.Height = 330;
        guests.BackgroundColor = AppTheme.Surface;
        guests.BorderStyle = BorderStyle.FixedSingle;
        guests.RowHeadersVisible = false;
        guests.AllowUserToAddRows = true;
        guests.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        guests.EnableHeadersVisualStyles = false;
        guests.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.MintSoft;
        guests.ColumnHeadersDefaultCellStyle.Font = AppTheme.SmallBold;
        guests.RowTemplate.Height = 30;
        guests.Font = AppTheme.Body;

        var room = new DataGridViewComboBoxColumn { Name = "room", HeaderText = "Phòng", FillWeight = 50, FlatStyle = FlatStyle.Flat };
        foreach (var r in booking.Rooms) room.Items.Add(r.RoomNumber);
        var gender = new DataGridViewComboBoxColumn { Name = "gender", HeaderText = "Giới tính", FillWeight = 50, FlatStyle = FlatStyle.Flat };
        foreach (var g in Genders) gender.Items.Add(g);
        var doc = new DataGridViewComboBoxColumn { Name = "doctype", HeaderText = "Loại giấy tờ", FillWeight = 60, FlatStyle = FlatStyle.Flat };
        foreach (var t in DocTypes) doc.Items.Add(t);

        guests.Columns.Add(room);
        guests.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Họ tên", FillWeight = 120 });
        guests.Columns.Add(new DataGridViewTextBoxColumn { Name = "birth", HeaderText = "Ngày sinh", FillWeight = 60 });
        guests.Columns.Add(gender);
        guests.Columns.Add(doc);
        guests.Columns.Add(new DataGridViewTextBoxColumn { Name = "docno", HeaderText = "Số giấy tờ", FillWeight = 80 });
        guests.Columns.Add(new DataGridViewTextBoxColumn { Name = "nation", HeaderText = "Quốc tịch", FillWeight = 60 });
        guests.Columns.Add(new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "Đại diện", FillWeight = 40 });
        guests.DefaultValuesNeeded += (_, e) =>
        {
            e.Row.Cells["room"].Value = booking.Rooms.FirstOrDefault()?.RoomNumber;
            e.Row.Cells["doctype"].Value = "CCCD";
            e.Row.Cells["nation"].Value = "Việt Nam";
            e.Row.Cells["rep"].Value = false;
        };
        guests.DataError += (_, e) => e.ThrowException = false;

        var roomOf = booking.Rooms.ToDictionary(r => r.DetailId, r => r.RoomNumber);
        if (booking.Guests.Count > 0)
        {
            foreach (var g in booking.Guests)
                guests.Rows.Add(roomOf.GetValueOrDefault(g.DetailId, booking.Rooms[0].RoomNumber), g.FullName,
                    g.BirthDate?.ToString("dd/MM/yyyy") ?? "", Genders.Contains(g.Gender ?? "") ? g.Gender ?? "" : "",
                    DocTypes.Contains(g.DocumentType ?? "") ? g.DocumentType ?? "" : "CCCD", g.DocumentNumber ?? "", g.Nationality ?? "", g.IsRepresentative);
        }
        else
        {
            // Gợi ý: khách đặt phòng là người đại diện ở phòng đầu tiên.
            var first = true;
            foreach (var r in booking.Rooms)
            {
                guests.Rows.Add(r.RoomNumber, first ? booking.Customer.FullName : "", "", "", "CCCD",
                    first ? booking.Customer.IdentityNumber ?? "" : "", "Việt Nam", first);
                first = false;
            }
        }
    }

    private async Task SaveAsync()
    {
        var detailOf = booking.Rooms.ToDictionary(r => r.RoomNumber, r => r.DetailId);
        var list = new List<StayGuestDto>();
        foreach (DataGridViewRow row in guests.Rows)
        {
            if (row.IsNewRow) continue;
            var name = (row.Cells["name"].Value as string ?? "").Trim();
            if (name.Length == 0) continue;
            var roomNumber = row.Cells["room"].Value as string ?? "";
            if (!detailOf.TryGetValue(roomNumber, out var detailId)) { Ui.Error(this, $"Khách {name}: chọn phòng."); return; }
            DateOnly? birth = null;
            var birthText = (row.Cells["birth"].Value as string ?? "").Trim();
            if (birthText.Length > 0)
            {
                if (!DateOnly.TryParseExact(birthText, "dd/MM/yyyy", out var b)) { Ui.Error(this, $"Khách {name}: ngày sinh không đúng dạng dd/MM/yyyy."); return; }
                birth = b;
            }
            list.Add(new StayGuestDto(null, detailId, name, birth, Empty(row.Cells["gender"].Value as string),
                Empty(row.Cells["doctype"].Value as string), Empty(row.Cells["docno"].Value as string),
                Empty(row.Cells["nation"].Value as string), row.Cells["rep"].Value is true));
        }
        var missingRooms = booking.Rooms.Where(r => list.All(g => g.DetailId != r.DetailId)).Select(r => r.RoomNumber).ToList();
        if (missingRooms.Count > 0 && !Ui.Confirm(this, $"Phòng {string.Join(", ", missingRooms)} chưa khai báo khách nào. Vẫn nhận phòng?")) return;

        var ok = await RunAsync(() => api.CheckInAsync(booking.Summary.Id, new DeskCheckInRequest(list)));
        if (!ok) return;
        Ui.Info(this, $"Đã nhận phòng cho booking {booking.Summary.BookingCode}. Phòng chuyển sang Đang ở.");
        CloseOk();
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Chốt hóa đơn, xác nhận thu đủ tiền rồi mới trả phòng.</summary>
public sealed class DlgCheckOut : DialogBase
{
    private readonly ApiClient api;
    private readonly FrontDeskBookingDetail booking;
    private readonly NumericUpDown surcharge = Inputs.Number(0, 0, 1_000_000_000, 2, 50000);
    private readonly NumericUpDown vat = Inputs.Number(10, 0, 100, 2, 1);
    private readonly ComboBox method = Inputs.Combo([]);
    private readonly Label summary = Inputs.Caption("Đang tải…", AppTheme.Body, AppTheme.TextSoft);
    private readonly Label totals = Inputs.Caption("", AppTheme.KpiSmall);
    private readonly Label notes = Inputs.Caption("", AppTheme.Small, AppTheme.WarningFg);
    private CheckOutPreview? preview;
    private InvoiceLine? invoice;
    private InvoicePayment? pendingPayment;

    public DlgCheckOut(ApiClient api, FrontDeskBookingDetail booking)
        : base("Thanh toán và trả phòng", 740, 590, $"{booking.Summary.BookingCode} · {booking.Customer.FullName}")
    {
        this.api = api;
        this.booking = booking;
        summary.AutoSize = false;
        summary.Height = 100;
        notes.AutoSize = false;
        notes.Height = 55;
        totals.AutoSize = false;
        totals.Height = 70;
        var grid = new FormGrid(2);
        grid.AddFull(summary);
        grid.Add("Phụ thu nhập thêm (đ)", surcharge);
        grid.Add("Thuế VAT (%)", vat);
        grid.Add("Phương thức thu tiền", method);
        grid.AddFull(totals);
        grid.AddFull(notes);
        Body.Controls.Add(grid);
        surcharge.ValueChanged += (_, _) => Recalc();
        vat.ValueChanged += (_, _) => Recalc();
        AddButton("1. Chốt hóa đơn", false, async (_, _) => await PrepareAsync());
        AddButton("2. Xác nhận thu tiền", false, async (_, _) => await PayAsync());
        AddButton("3. Trả phòng", true, async (_, _) => await SaveAsync());
        AddCancelButton();
        Shown += async (_, _) => await RunAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var detail = await api.GetBookingDetailAsync(booking.Summary.Id);
        invoice = detail.Invoices.SingleOrDefault(x => x.Status is not ("NHAP" or "DA_HUY"));
        preview = await api.GetCheckOutPreviewAsync(booking.Summary.Id);
        DlgDeposit.FillMethods(method, await api.GetPaymentMethodsAsync());
        surcharge.Enabled = vat.Enabled = invoice is null;
        summary.Text = $"Tiền phòng: {Ui.Money(preview.RoomAmount)}\n" +
            $"Phụ thu tự động: {Ui.Money(preview.EarlyCheckInSurcharge + preview.LateCheckOutSurcharge)}\n" +
            $"Bồi thường: {Ui.Money(preview.Compensation)} · Cọc áp dụng: {Ui.Money(preview.Deposit)}";
        notes.Text = invoice is null ? "Chốt hóa đơn trước khi thu tiền. Số tiền chốt có thể thay đổi nếu phát sinh trả trễ."
            : "Hóa đơn đã chốt. Nếu cần sửa, phải hủy và lập lại bằng nghiệp vụ hóa đơn.";
        Recalc();
    }

    private void Recalc()
    {
        if (preview is null) return;
        var taxable = preview.RoomAmount + preview.EarlyCheckInSurcharge + preview.LateCheckOutSurcharge + surcharge.Value + preview.Compensation;
        var total = invoice?.Total ?? taxable + Math.Round(taxable * vat.Value / 100m, 2, MidpointRounding.AwayFromZero);
        var due = invoice?.Due ?? Math.Max(0, total - preview.Deposit);
        totals.Text = $"Tổng {Ui.Money(total)} · Còn thu {Ui.Money(due)}";
    }

    private async Task PrepareAsync()
    {
        if (preview is null) return;
        if (!Ui.Confirm(this, "Chốt hóa đơn với phụ thu và VAT đang hiển thị?")) return;
        await RunAsync(async () =>
        {
            // Đọc lại trước khi gửi để tiếp tục được nếu lần chốt trước mất phản hồi.
            await LoadAsync();
            if (invoice is null) await api.PrepareInvoiceAsync(booking.Summary.Id, surcharge.Value, vat.Value / 100m);
            await LoadAsync();
        });
    }

    private async Task PayAsync()
    {
        if (invoice is null) { Ui.Error(this, "Hãy chốt hóa đơn trước."); return; }
        if (pendingPayment is null)
        {
            if (invoice.Due <= 0) { Ui.Info(this, "Hóa đơn đã thanh toán đủ."); return; }
            if (method.SelectedItem is not Choice<long> selected) { Ui.Error(this, "Chọn phương thức thu tiền."); return; }
            if (!Ui.Confirm(this, $"Xác nhận đã nhận {Ui.Money(invoice.Due)} bằng phương thức đã chọn?")) return;
            pendingPayment = new InvoicePayment(selected.Value, invoice.Due, Guid.NewGuid().ToString("N"));
        }
        await RunAsync(async () =>
        {
            // Giữ nguyên mã, phương thức và số tiền khi thử lại sau lỗi kết nối.
            await api.PayInvoiceAsync(invoice.Id, pendingPayment);
            pendingPayment = null;
            await LoadAsync();
        });
    }

    private async Task SaveAsync()
    {
        if (invoice is null || invoice.Due > 0) { Ui.Error(this, "Cần chốt và thanh toán đủ hóa đơn trước khi trả phòng."); return; }
        if (!Ui.Confirm(this, "Xác nhận trả phòng và chuyển phòng sang Cần dọn?")) return;
        if (await RunAsync(async () =>
        {
            var result = await api.CheckOutAsync(booking.Summary.Id, new DeskCheckOutRequest(0));
            Ui.Info(this, result.Message);
        })) CloseOk();
    }
}
