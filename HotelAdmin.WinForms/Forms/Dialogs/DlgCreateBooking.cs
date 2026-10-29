using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Tạo booking tại quầy (mục 7.2): chọn ngày → tìm phòng trống → chọn phòng (xem giá từng đêm) →
/// chọn khách có sẵn hoặc nhập khách vãng lai → (tùy chọn) thu cọc để xác nhận ngay (mục 7.3).
/// Không thu cọc: booking ở trạng thái Giữ chỗ 15 phút theo dịch vụ booking dùng chung.
/// </summary>
public sealed class DlgCreateBooking : DialogBase
{
    private readonly ApiClient api;

    // Thời gian, số khách
    private readonly DateTimePicker checkIn = Inputs.Date(DateTime.Today.AddHours(14), true);
    private readonly DateTimePicker checkOut = Inputs.Date(DateTime.Today.AddDays(1).AddHours(12), true);
    private readonly NumericUpDown adults = Inputs.Number(2, 1, 50);
    private readonly NumericUpDown children = Inputs.Number(0, 0, 50);

    // Phòng
    private readonly DataGridView rooms = new();
    private readonly Label selection = Inputs.Caption("Chưa chọn phòng.", AppTheme.BodyBold);
    private List<AvailableRoom> available = [];
    private decimal total;
    private int priceVersion;

    // Khách
    private readonly RadioButton existingCustomer = new() { Text = "Khách có sẵn", Checked = true, AutoSize = true };
    private readonly RadioButton newCustomer = new() { Text = "Khách mới / vãng lai", AutoSize = true };
    private readonly TextBox customerSearch = Inputs.Text();
    private readonly ListBox customerList = new() { Height = 120, Font = AppTheme.Body, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox newName = Inputs.Text();
    private readonly TextBox newPhone = Inputs.Text();
    private readonly TextBox newEmail = Inputs.Text();
    private readonly TextBox newIdentity = Inputs.Text();
    private readonly Panel existingPanel = new() { Height = 190 };
    private readonly Panel newPanel = new() { Height = 190, Visible = false };

    // Khác
    private readonly ComboBox channel = Inputs.Combo([new Choice<string>("Tại quầy", "QUAY"), new Choice<string>("Điện thoại", "DIEN_THOAI")]);
    private readonly TextBox special = Inputs.Text(null, true, 56);
    private readonly CheckBox takeDeposit = new() { Text = "Thu cọc ngay (xác nhận booking)", AutoSize = true, Checked = true };
    private readonly ComboBox method = Inputs.Combo([]);
    private readonly NumericUpDown deposit = Inputs.Number(0, 0, 10_000_000_000, 0, 50000);

    private CancellationTokenSource? searchCts;

    public DlgCreateBooking(ApiClient api)
        : base("Tạo booking tại quầy", 1120, 760, "Booking đủ cọc được xác nhận ngay; chưa cọc sẽ giữ phòng 15 phút.")
    {
        this.api = api;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = AppTheme.Surface };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(BuildLeft(), 0, 0);
        layout.Controls.Add(BuildRight(), 1, 0);
        Body.AutoScroll = false;
        Body.Controls.Add(layout);

        AddButton("Tạo booking", true, async (_, _) => await SaveAsync());
        AddCancelButton();
        Shown += async (_, _) =>
        {
            await RunAsync(async () => DlgDeposit.FillMethods(method, await api.GetPaymentMethodsAsync()));
            await SearchRoomsAsync();
            await SearchCustomersAsync();
        };
    }

    /// <summary>Booking vừa tạo (sau khi OK).</summary>
    public FrontDeskBookingDetail? CreatedBooking { get; private set; }

    // =====================================================================
    private Control BuildLeft()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 18, 0), BackColor = AppTheme.Surface };
        var top = new FormGrid(4);
        top.Add("Nhận phòng", checkIn);
        top.Add("Trả phòng", checkOut);
        top.Add("Người lớn", adults);
        top.Add("Trẻ em", children);
        var find = new FlatButton { Text = "Tìm phòng trống", IconGlyph = Glyph.Search, Size = new Size(170, 36) };
        find.Click += async (_, _) => await SearchRoomsAsync();
        top.AddFull(find);
        find.Anchor = AnchorStyles.Left | AnchorStyles.Top;

        rooms.Dock = DockStyle.Fill;
        rooms.BackgroundColor = AppTheme.Surface;
        rooms.BorderStyle = BorderStyle.FixedSingle;
        rooms.RowHeadersVisible = false;
        rooms.AllowUserToAddRows = false;
        rooms.AllowUserToDeleteRows = false;
        rooms.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        rooms.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        rooms.EnableHeadersVisualStyles = false;
        rooms.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.MintSoft;
        rooms.ColumnHeadersDefaultCellStyle.Font = AppTheme.SmallBold;
        rooms.RowTemplate.Height = 32;
        rooms.Columns.Add(new DataGridViewCheckBoxColumn { Name = "pick", HeaderText = "Chọn", FillWeight = 30 });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "room", HeaderText = "Phòng", FillWeight = 40, ReadOnly = true });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "floor", HeaderText = "Tầng", FillWeight = 30, ReadOnly = true });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Loại phòng", FillWeight = 70, ReadOnly = true });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "cap", HeaderText = "Sức chứa", FillWeight = 55, ReadOnly = true });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "price", HeaderText = "Giá/đêm", FillWeight = 60, ReadOnly = true, DefaultCellStyle = { Format = "#,0", Alignment = DataGridViewContentAlignment.MiddleRight } });
        rooms.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Hiện trạng", FillWeight = 60, ReadOnly = true });
        rooms.CurrentCellDirtyStateChanged += (_, _) => { if (rooms.IsCurrentCellDirty) rooms.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        rooms.CellValueChanged += async (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0) await RecalcAsync(); };

        selection.AutoSize = false;
        selection.Height = 44;
        selection.Dock = DockStyle.Bottom;

        panel.Controls.Add(rooms);
        panel.Controls.Add(selection);
        panel.Controls.Add(top);
        return panel;
    }

    private Control BuildRight()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8, 0, 0, 0), BackColor = AppTheme.Surface };
        var grid = new FormGrid(2);

        var radios = new FlowLayoutPanel { Height = 30, AutoSize = false, WrapContents = false, BackColor = AppTheme.Surface };
        radios.Controls.Add(existingCustomer);
        radios.Controls.Add(newCustomer);
        grid.Add("Khách hàng", radios, 2);

        // Khách có sẵn: ô tìm + danh sách
        customerSearch.PlaceholderText = "Tên, số điện thoại, email, CCCD…";
        customerSearch.Dock = DockStyle.Top;
        customerList.Dock = DockStyle.Top;
        customerList.Top = 36;
        existingPanel.Controls.Add(customerList);
        existingPanel.Controls.Add(customerSearch);
        customerSearch.TextChanged += async (_, _) => await SearchCustomersAsync();
        grid.Add("Tìm khách", existingPanel, 2);

        // Khách mới
        var newGrid = new FormGrid(2);
        newGrid.Add("Họ tên *", newName, 2);
        newGrid.Add("Số điện thoại", newPhone);
        newGrid.Add("CCCD / Hộ chiếu", newIdentity);
        newGrid.Add("Email", newEmail, 2);
        newPanel.Controls.Add(newGrid);
        grid.Add("Thông tin khách mới", newPanel, 2);

        existingCustomer.CheckedChanged += (_, _) =>
        {
            existingPanel.Visible = existingCustomer.Checked;
            newPanel.Visible = !existingCustomer.Checked;
        };

        grid.Add("Kênh đặt", channel);
        grid.Add("Phương thức cọc", method);
        grid.AddFull(takeDeposit);
        grid.Add("Số tiền cọc", deposit);
        grid.Add("Yêu cầu đặc biệt", special, 2);
        takeDeposit.CheckedChanged += (_, _) => { method.Enabled = deposit.Enabled = takeDeposit.Checked; };

        panel.Controls.Add(grid);
        return panel;
    }

    // =====================================================================
    private async Task SearchRoomsAsync()
    {
        var inDate = DateOnly.FromDateTime(checkIn.Value);
        var outDate = DateOnly.FromDateTime(checkOut.Value);
        if (outDate <= inDate) { Ui.Error(this, "Ngày trả phòng phải sau ngày nhận phòng."); return; }
        await RunAsync(async () =>
        {
            available = await api.GetAvailableRoomsAsync(inDate, outDate);
            rooms.Rows.Clear();
            foreach (var r in available)
                rooms.Rows.Add(false, r.RoomNumber, r.Floor ?? "", r.RoomTypeName, $"{r.MaxAdults} NL + {r.MaxChildren} TE",
                    r.BaseNightlyPrice + r.NightSurcharge, StatusStyles.ForRoom(r.Status).Label);
            total = 0;
            selection.Text = available.Count == 0 ? "Không còn phòng trống trong khoảng ngày này." : $"{available.Count} phòng trống. Tích chọn phòng để xem giá.";
        });
    }

    private List<AvailableRoom> PickedRooms()
    {
        var list = new List<AvailableRoom>();
        foreach (DataGridViewRow row in rooms.Rows)
            if (row.Cells["pick"].Value is true && row.Index < available.Count) list.Add(available[row.Index]);
        return list;
    }

    /// <summary>Tính giá từng đêm của các phòng đã chọn bằng API xem trước giá (theo chính sách giá).</summary>
    private async Task RecalcAsync()
    {
        var picked = PickedRooms();
        var version = ++priceVersion;
        if (picked.Count == 0)
        {
            total = 0;
            selection.Text = "Chưa chọn phòng.";
            return;
        }
        var inDate = DateOnly.FromDateTime(checkIn.Value);
        var outDate = DateOnly.FromDateTime(checkOut.Value);
        selection.Text = "Đang tính giá…";
        try
        {
            decimal sum = 0;
            foreach (var r in picked) sum += (await api.PreviewPriceAsync(r.Id, inDate, outDate)).Total;
            if (version != priceVersion) return;
            total = sum;
            var capacity = picked.Sum(r => r.MaxAdults);
            var warn = capacity < adults.Value ? $"  ·  ⚠ sức chứa {capacity} người lớn" : "";
            selection.Text = $"Đã chọn {string.Join(", ", picked.Select(r => r.RoomNumber))}  ·  {(outDate.DayNumber - inDate.DayNumber)} đêm  ·  Tổng {Ui.Money(total)}{warn}";
            deposit.Maximum = Math.Max(deposit.Minimum, total);
            deposit.Value = Math.Min(deposit.Maximum, DlgDeposit.SuggestDeposit(total));
        }
        catch (ApiException ex)
        {
            selection.Text = "Không tính được giá: " + ex.Message;
        }
    }

    private async Task SearchCustomersAsync()
    {
        searchCts?.Cancel();
        searchCts = new CancellationTokenSource();
        var token = searchCts.Token;
        try
        {
            await Task.Delay(250, token); // chờ người dùng gõ xong
            var list = await api.FindCustomersAsync(customerSearch.Text, token);
            if (token.IsCancellationRequested) return;
            customerList.Items.Clear();
            foreach (var c in list)
                customerList.Items.Add(new Choice<FrontDeskCustomer>($"{c.FullName}  ·  {c.Phone ?? "—"}  ·  {c.Stays} lần ở", c));
        }
        catch (TaskCanceledException) { }
        catch (ApiException ex) when (!token.IsCancellationRequested)
        {
            customerList.Items.Clear();
            customerList.Items.Add(ex.Message);
        }
        catch (ApiException)
        {
            // Lượt tìm cũ bị hủy vì người dùng gõ tiếp: bỏ qua.
        }
    }

    private async Task SaveAsync()
    {
        var picked = PickedRooms();
        if (picked.Count == 0) { Ui.Error(this, "Hãy chọn ít nhất một phòng."); return; }

        long? customerId = null;
        NewCustomerRequest? newCus = null;
        if (existingCustomer.Checked)
        {
            if (customerList.SelectedItem is not Choice<FrontDeskCustomer> c) { Ui.Error(this, "Chọn khách trong danh sách hoặc chuyển sang Khách mới."); return; }
            customerId = c.Value.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newName.Text)) { Ui.Error(this, "Nhập họ tên khách."); return; }
            if (string.IsNullOrWhiteSpace(newPhone.Text) && string.IsNullOrWhiteSpace(newEmail.Text))
            {
                Ui.Error(this, "Nhập số điện thoại hoặc email để liên hệ khách.");
                return;
            }
            newCus = new NewCustomerRequest(newName.Text.Trim(), newPhone.Text, newEmail.Text, newIdentity.Text, null);
        }

        DeskDepositRequest? dep = null;
        if (takeDeposit.Checked && deposit.Value > 0)
        {
            if (method.SelectedItem is not Choice<long> m) { Ui.Error(this, "Chọn phương thức thu cọc."); return; }
            dep = new DeskDepositRequest(m.Value, deposit.Value);
        }

        var ch = channel.SelectedItem is Choice<string> cc ? cc.Value : "QUAY";
        var request = new CreateDeskBookingRequest(customerId, newCus, picked.Select(r => r.Id).ToList(), checkIn.Value, checkOut.Value,
            (int)adults.Value, (int)children.Value, ch, special.Text, dep);

        var ok = await RunAsync(async () => CreatedBooking = await api.CreateDeskBookingAsync(request));
        if (!ok || CreatedBooking is null) return;
        Ui.Info(this, $"Đã tạo booking {CreatedBooking.Summary.BookingCode} ({StatusStyles.ForBooking(CreatedBooking.Summary.Status).Label}).\nTổng tiền phòng {Ui.Money(CreatedBooking.Summary.Total)}.");
        CloseOk();
    }
}
