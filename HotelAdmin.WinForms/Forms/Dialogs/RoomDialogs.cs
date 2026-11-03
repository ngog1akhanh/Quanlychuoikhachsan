using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>
/// Xem / sửa một phòng (mục 9.2) và đổi trạng thái vận hành (mục 9.3). room = null → thêm phòng mới.
/// </summary>
public sealed class DlgRoom : DialogBase
{
    private static readonly string[] ManualStatuses = ["SAN_SANG", "CAN_DON", "DANG_DON", "BAO_TRI", "NGUNG_SU_DUNG"];

    private readonly ApiClient api;
    private readonly UserSession session;
    private readonly RoomBoardItem? room;
    private readonly TextBox code = Inputs.Text();
    private readonly TextBox number = Inputs.Text();
    private readonly ComboBox type;
    private readonly TextBox floor = Inputs.Text();
    private readonly TextBox direction = Inputs.Text();
    private readonly TextBox view = Inputs.Text();
    private readonly TextBox position = Inputs.Text();
    private readonly NumericUpDown surcharge = Inputs.Number(0, 0, 100_000_000, 0, 50000);
    private readonly TextBox description = Inputs.Text(null, true, 56);
    private readonly TextBox note = Inputs.Text(null, true, 56);
    private readonly ComboBox status = Inputs.Combo(ManualStatuses.Select(s => (object)new Choice<string>(StatusStyles.ForRoom(s).Label, s)));

    public DlgRoom(ApiClient api, UserSession session, RoomBoardItem? room, List<RoomTypeSummary> types)
        : base(room is null ? "Thêm phòng" : $"Phòng {room.RoomNumber}", 640, room is null ? 560 : 680,
            room is null ? "Phòng mới mặc định ở trạng thái Sẵn sàng." : $"{room.RoomTypeName} · {StatusStyles.ForRoom(room.Status).Label}")
    {
        this.api = api;
        this.session = session;
        this.room = room;
        type = Inputs.Combo(types.Select(t => (object)new Choice<long>($"{t.Name} ({t.Code}) · {t.BaseNightlyPrice:#,0} đ", t.Id)));

        var grid = new FormGrid(2);
        if (room is null)
        {
            grid.Add("Mã phòng *", code);
            grid.Add("Số phòng *", number);
            grid.Add("Loại phòng *", type, 2);
            grid.Add("Tầng", floor);
            grid.Add("View", view);
            grid.Add("Phụ thu mỗi đêm (đ)", surcharge);
        }
        else
        {
            number.Text = room.RoomNumber;
            floor.Text = room.Floor ?? "";
            direction.Text = room.Direction ?? "";
            view.Text = room.View ?? "";
            position.Text = room.Position ?? "";
            surcharge.Value = Math.Min(surcharge.Maximum, room.NightSurcharge);
            description.Text = room.Description ?? "";
            note.Text = room.Note ?? "";
            SelectChoice(type, room.RoomTypeId);

            if (room.CurrentBookingCode is not null)
                grid.AddFull(Inputs.Caption($"Đang ở: {room.CurrentGuest} · booking {room.CurrentBookingCode} · trả {room.CurrentCheckOut:dd/MM HH:mm}", AppTheme.BodyBold, AppTheme.Forest));
            if (room.ArrivalBookingCode is not null)
                grid.AddFull(Inputs.Caption($"Khách đến hôm nay: {room.ArrivalGuest} · booking {room.ArrivalBookingCode} · {room.ArrivalTime:HH:mm}", AppTheme.Body, AppTheme.InfoFg));

            // Đổi trạng thái vận hành
            var statusRow = new FlowLayoutPanel { Height = 40, WrapContents = false, BackColor = AppTheme.Surface };
            status.Width = 220;
            var setStatus = new FlatButton { Text = "Đổi trạng thái", Size = new Size(140, 34), Margin = new Padding(8, 0, 0, 0) };
            setStatus.Click += async (_, _) => await SetStatusAsync();
            statusRow.Controls.Add(status);
            statusRow.Controls.Add(setStatus);
            SelectChoice(status, room.Status);
            var canStatus = session.HasPermission("ROOM_WRITE") && room.Status != "DANG_O";
            status.Enabled = setStatus.Enabled = canStatus;
            grid.Add(room.Status == "DANG_O" ? "Trạng thái vận hành (đang có khách: đổi qua trả phòng / đổi phòng)" : "Trạng thái vận hành", statusRow, 2);

            grid.Add("Số phòng", number);
            grid.Add("Loại phòng", type);
            grid.Add("Tầng", floor);
            grid.Add("Hướng", direction);
            grid.Add("View", view);
            grid.Add("Vị trí", position);
            grid.Add("Phụ thu mỗi đêm (đ)", surcharge);
            grid.Add("Mã phòng", Inputs.Caption(room.RoomCode, AppTheme.Body, AppTheme.TextMuted));
            grid.Add("Mô tả", description, 2);
            grid.Add("Ghi chú vận hành", note, 2);

            if (room.CurrentBookingId is not null || room.ArrivalBookingId is not null)
            {
                var bookingId = room.CurrentBookingId ?? room.ArrivalBookingId!.Value;
                AddButton("Xem booking", false, (_, _) =>
                {
                    using var dlg = new DlgBookingDetail(api, session, bookingId);
                    dlg.ShowDialog(this);
                    if (dlg.Changed) DialogResult = DialogResult.OK;
                });
            }
        }
        Body.Controls.Add(grid);

        var canWrite = session.HasPermission("ROOM_WRITE");
        if (canWrite) AddButton(room is null ? "Thêm phòng" : "Lưu thông tin", true, async (_, _) => await SaveAsync());
        AddCancelButton(canWrite ? "Hủy" : "Đóng");
    }

    private static void SelectChoice<T>(ComboBox combo, T value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is Choice<T> c && EqualityComparer<T>.Default.Equals(c.Value, value)) { combo.SelectedIndex = i; return; }
    }

    private async Task SetStatusAsync()
    {
        if (room is null || status.SelectedItem is not Choice<string> s) return;
        if (s.Value == room.Status) { Ui.Info(this, "Phòng đã ở trạng thái này."); return; }
        var ok = await RunAsync(() => api.SetRoomStatusAsync(room.Id, s.Value, string.IsNullOrWhiteSpace(note.Text) ? null : note.Text));
        if (!ok) return;
        Ui.Info(this, $"Phòng {room.RoomNumber} chuyển sang {s.Text}.");
        CloseOk();
    }

    private async Task SaveAsync()
    {
        if (type.SelectedItem is not Choice<long> t) { Ui.Error(this, "Chọn loại phòng."); return; }
        if (string.IsNullOrWhiteSpace(number.Text)) { Ui.Error(this, "Nhập số phòng."); return; }
        bool ok;
        if (room is null)
        {
            if (string.IsNullOrWhiteSpace(code.Text)) { Ui.Error(this, "Nhập mã phòng (ví dụ P501)."); return; }
            ok = await RunAsync(() => api.CreateRoomAsync(new CreateRoomRequest(code.Text.Trim(), number.Text.Trim(), t.Value,
                Clean(floor.Text), Clean(view.Text), surcharge.Value)));
        }
        else
        {
            ok = await RunAsync(() => api.UpdateRoomAsync(room.Id, new UpdateRoomRequest(number.Text.Trim(), t.Value, floor.Text,
                direction.Text, view.Text, position.Text, surcharge.Value, description.Text, note.Text)));
        }
        if (ok) CloseOk();
    }

    private static string? Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Thêm / sửa loại phòng (mục 9.1). Đổi giá cơ bản không ảnh hưởng booking cũ (giá đã chốt).</summary>
public sealed class DlgRoomType : DialogBase
{
    private readonly ApiClient api;
    private readonly RoomTypeSummary? current;
    private readonly TextBox code = Inputs.Text();
    private readonly TextBox name = Inputs.Text();
    private readonly NumericUpDown adults = Inputs.Number(2, 1, 20);
    private readonly NumericUpDown children = Inputs.Number(0, 0, 20);
    private readonly NumericUpDown area = Inputs.Number(25, 1, 1000, 1, 1);
    private readonly NumericUpDown price = Inputs.Number(800000, 0, 1_000_000_000, 0, 50000);
    private readonly TextBox description = Inputs.Text(null, true, 70);
    private readonly CheckBox active = new() { Text = "Đang kinh doanh", AutoSize = true, Checked = true };

    public DlgRoomType(ApiClient api, RoomTypeSummary? current)
        : base(current is null ? "Thêm loại phòng" : $"Loại phòng {current.Name}", 600, 520,
            current is null ? null : $"{current.RoomCount} phòng · {current.OccupiedCount} đang ở")
    {
        this.api = api;
        this.current = current;
        var grid = new FormGrid(2);
        if (current is null) grid.Add("Mã loại *", code);
        grid.Add("Tên loại phòng *", name, current is null ? 1 : 2);
        grid.Add("Người lớn tối đa", adults);
        grid.Add("Trẻ em tối đa", children);
        grid.Add("Giá cơ bản / đêm (đ)", price);
        if (current is not null) grid.Add("Diện tích (m²)", area);
        grid.Add("Mô tả", description, 2);
        if (current is not null) grid.AddFull(active);

        if (current is not null)
        {
            name.Text = current.Name;
            adults.Value = Math.Min(adults.Maximum, current.MaxAdults);
            children.Value = Math.Min(children.Maximum, current.MaxChildren);
            area.Value = Math.Min(area.Maximum, Math.Max(area.Minimum, current.AreaM2 ?? 25));
            price.Value = Math.Min(price.Maximum, current.BaseNightlyPrice);
            description.Text = current.Description ?? "";
            active.Checked = current.Status == "HOAT_DONG";
        }
        Body.Controls.Add(grid);
        AddButton(current is null ? "Thêm loại phòng" : "Lưu", true, async (_, _) => await SaveAsync());
        AddCancelButton();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { Ui.Error(this, "Nhập tên loại phòng."); return; }
        bool ok;
        if (current is null)
        {
            if (string.IsNullOrWhiteSpace(code.Text)) { Ui.Error(this, "Nhập mã loại (ví dụ VIP)."); return; }
            ok = await RunAsync(() => api.CreateRoomTypeAsync(new CreateRoomTypeRequest(code.Text.Trim().ToUpperInvariant(), name.Text.Trim(),
                (int)adults.Value, (int)children.Value, price.Value, string.IsNullOrWhiteSpace(description.Text) ? null : description.Text.Trim())));
        }
        else
        {
            if (price.Value != current.BaseNightlyPrice &&
                !Ui.Confirm(this, "Đổi giá cơ bản chỉ áp dụng cho booking mới; booking đã đặt giữ giá đã chốt. Tiếp tục?")) return;
            ok = await RunAsync(() => api.UpdateRoomTypeAsync(current.Id, new UpdateRoomTypeRequest(name.Text.Trim(), (int)adults.Value,
                (int)children.Value, area.Value, price.Value, description.Text, active.Checked ? "HOAT_DONG" : "NGUNG_SU_DUNG")));
        }
        if (ok) CloseOk();
    }
}
