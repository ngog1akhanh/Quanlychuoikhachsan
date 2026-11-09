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
    // Chỉ dùng khi thêm mới: tạo kèm phòng.
    private readonly NumericUpDown roomCount = Inputs.Number(0, 0, 50);
    private readonly TextBox firstNumber = Inputs.Text();
    private readonly TextBox floor = Inputs.Text();
    // Ảnh chọn từ máy (thêm mới và sửa): API lưu vào thư mục photos của web khách, tên {mã loại}-{số}.jpg.
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static string? lastPhotoFolder;
    private readonly List<string> photoFiles = [];
    private readonly Label photoInfo = new() { AutoSize = true, MaximumSize = new Size(300, 46), Font = AppTheme.Small, ForeColor = AppTheme.TextMuted, Margin = new Padding(10, 8, 0, 0), Text = "Chưa chọn ảnh" };

    public DlgRoomType(ApiClient api, RoomTypeSummary? current)
        : base(current is null ? "Thêm loại phòng" : $"Loại phòng {current.Name}", 600, current is null ? 720 : 600,
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
        grid.Add("Diện tích (m²)", area);
        grid.Add("Mô tả", description, 2);
        grid.Add("Ảnh loại phòng (JPG / PNG / WEBP, tối đa 5 MB/ảnh)", PhotoPicker(), 2);
        if (current is not null) grid.AddFull(active);
        else
        {
            // Loại phòng chỉ hiện trên web khi có phòng thuộc loại đó → cho tạo phòng ngay tại đây.
            grid.Add("Số phòng tạo kèm", roomCount);
            grid.Add("Số phòng đầu tiên (vd 501)", firstNumber);
            grid.Add("Tầng", floor);
            grid.AddFull(new Label
            {
                Text = "Phòng tạo kèm có mã P + số phòng (P501, P502…), trạng thái Sẵn sàng. Loại phòng chỉ hiện trên web khi có ít nhất 1 phòng.",
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                Font = AppTheme.Small,
                ForeColor = AppTheme.TextMuted
            });
        }

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
        if (current is not null) Shown += async (_, _) => await ShowExistingPhotosAsync(current.Id);
    }

    // ---------------- Ảnh loại phòng ----------------
    private Control PhotoPicker()
    {
        var row = new FlowLayoutPanel { Height = 52, WrapContents = false, BackColor = AppTheme.Surface };
        var choose = new FlatButton { Text = "Chọn ảnh từ máy…", Size = new Size(160, 34), Margin = new Padding(0) };
        choose.Click += (_, _) => ChoosePhotos();
        var clear = new FlatButton { Text = "Bỏ chọn", Size = new Size(90, 34), Margin = new Padding(8, 0, 0, 0), FillColor = AppTheme.Surface, BorderColor = AppTheme.Border };
        clear.Click += (_, _) => { photoFiles.Clear(); UpdatePhotoInfo(); };
        row.Controls.Add(choose);
        row.Controls.Add(clear);
        row.Controls.Add(photoInfo);
        return row;
    }

    private void ChoosePhotos()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh loại phòng",
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
            Multiselect = true,
            InitialDirectory = lastPhotoFolder ?? DefaultPhotoFolder()
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        lastPhotoFolder = Path.GetDirectoryName(dialog.FileNames.FirstOrDefault());
        var rejected = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            var info = new FileInfo(file);
            if (!PhotoExtensions.Contains(info.Extension, StringComparer.OrdinalIgnoreCase)) rejected.Add($"{info.Name}: không phải JPG/PNG/WEBP");
            else if (info.Length > MaxPhotoBytes) rejected.Add($"{info.Name}: lớn hơn 5 MB");
            else if (!photoFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) photoFiles.Add(file);
        }
        if (photoFiles.Count > 10)
        {
            rejected.Add($"Mỗi lần chỉ tải tối đa 10 ảnh – đã bỏ {photoFiles.Count - 10} ảnh cuối.");
            photoFiles.RemoveRange(10, photoFiles.Count - 10);
        }
        UpdatePhotoInfo();
        if (rejected.Count > 0) Ui.Error(this, "Không dùng được:\n" + string.Join("\n", rejected));
    }

    private void UpdatePhotoInfo()
        => photoInfo.Text = photoFiles.Count == 0 ? "Chưa chọn ảnh"
            : $"{photoFiles.Count} ảnh: " + string.Join(", ", photoFiles.Select(f => Path.GetFileName(f)));

    /// <summary>
    /// Thư mục mở sẵn khi chọn ảnh: appsettings "PhotoFolder" → thư mục photos của web khách trong solution
    /// (QuanLyKhachSan.Web/wwwroot/images/rooms/photos) → thư mục Ảnh của Windows.
    /// </summary>
    private static string DefaultPhotoFolder()
    {
        var configured = AppConfig.Load().PhotoFolder;
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var photos = Path.Combine(dir.FullName, "QuanLyKhachSan.Web", "wwwroot", "images", "rooms", "photos");
            if (Directory.Exists(photos)) return photos;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private async Task ShowExistingPhotosAsync(long typeId)
    {
        try
        {
            var images = J.Items(await api.GetJsonAsync($"v1/room-types/{typeId}/images"));
            if (photoFiles.Count == 0 && images.Count > 0)
                photoInfo.Text = $"Đã có {images.Count} ảnh tải lên qua app. Chọn thêm để bổ sung.";
        }
        catch (ApiException) { /* chỉ là thông tin phụ */ }
    }

    private Task UploadPhotosAsync(long typeId)
        => api.UploadFilesAsync($"v1/room-types/{typeId}/images:upload", photoFiles);

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { Ui.Error(this, "Nhập tên loại phòng."); return; }
        bool ok;
        if (current is null)
        {
            if (string.IsNullOrWhiteSpace(code.Text)) { Ui.Error(this, "Nhập mã loại (ví dụ VIP)."); return; }
            var count = (int)roomCount.Value;
            var start = 0;
            if (count > 0 && !int.TryParse(firstNumber.Text.Trim(), out start)) { Ui.Error(this, "Nhập số phòng đầu tiên là số (ví dụ 501)."); return; }
            RoomTypeSummaryLite? created = null;
            ok = await RunAsync(async () =>
            {
                created = await api.CreateRoomTypeAsync(new CreateRoomTypeRequest(code.Text.Trim().ToUpperInvariant(), name.Text.Trim(),
                    (int)adults.Value, (int)children.Value, price.Value, string.IsNullOrWhiteSpace(description.Text) ? null : description.Text.Trim()));
            });
            if (!ok || created is null) return;
            // API tạo loại phòng chưa nhận diện tích → cập nhật ngay sau khi tạo (lỗi ở bước này không chặn việc tạo).
            try
            {
                await api.UpdateRoomTypeAsync(created.Id, new UpdateRoomTypeRequest(name.Text.Trim(), (int)adults.Value, (int)children.Value,
                    area.Value, price.Value, description.Text, "HOAT_DONG"));
            }
            catch (ApiException) { }
            await CreateExtrasAsync(created.Id, count, start);
        }
        else
        {
            if (price.Value != current.BaseNightlyPrice &&
                !Ui.Confirm(this, "Đổi giá cơ bản chỉ áp dụng cho booking mới; booking đã đặt giữ giá đã chốt. Tiếp tục?")) return;
            ok = await RunAsync(() => api.UpdateRoomTypeAsync(current.Id, new UpdateRoomTypeRequest(name.Text.Trim(), (int)adults.Value,
                (int)children.Value, area.Value, price.Value, description.Text, active.Checked ? "HOAT_DONG" : "NGUNG_SU_DUNG")));
            // Lỗi tải ảnh: giữ hộp thoại để thử lại (lưu thông tin ở trên chạy lại cũng không sao).
            if (ok && photoFiles.Count > 0)
            {
                ok = await RunAsync(() => UploadPhotosAsync(current.Id));
                if (ok) Ui.Info(this, $"Đã tải lên {photoFiles.Count} ảnh. Web khách hiện ảnh mới sau vài giây.");
            }
        }
        if (ok) CloseOk();
    }

    /// <summary>Tạo các phòng kèm theo và tải ảnh cho loại phòng mới; lỗi từng phần được gom lại để báo một lần.</summary>
    private async Task CreateExtrasAsync(long typeId, int count, int start)
    {
        var errors = new List<string>();
        var made = 0;
        UseWaitCursor = true;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var number = (start + i).ToString();
                try
                {
                    await api.CreateRoomAsync(new CreateRoomRequest("P" + number, number, typeId,
                        string.IsNullOrWhiteSpace(floor.Text) ? null : floor.Text.Trim(), null, 0));
                    made++;
                }
                catch (ApiException ex) { errors.Add($"Phòng {number}: {ex.Message}"); }
            }
            if (photoFiles.Count > 0)
            {
                try { await UploadPhotosAsync(typeId); }
                catch (ApiException ex) { errors.Add($"Ảnh: {ex.Message} (mở lại loại phòng để tải ảnh lần nữa)"); }
            }
        }
        finally { UseWaitCursor = false; }
        var summary = $"Đã thêm loại phòng {name.Text.Trim()}" + (count > 0 ? $" và {made}/{count} phòng." : ". Chưa có phòng nào – thêm phòng ở tab Sơ đồ phòng để loại này hiện trên web.")
            + (photoFiles.Count > 0 && errors.All(e => !e.StartsWith("Ảnh:")) ? $"\nĐã tải lên {photoFiles.Count} ảnh." : "");
        if (errors.Count > 0) Ui.Error(this, summary + "\n\nKhông tạo được:\n" + string.Join("\n", errors));
        else Ui.Info(this, summary);
    }
}
