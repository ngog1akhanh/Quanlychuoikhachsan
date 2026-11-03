using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;
using HotelAdmin.Services;
using HotelAdmin.Theme;

namespace HotelAdmin.Pages;

/// <summary>
/// Module Kho & tài sản (mục 15 + nhập hàng): mặt hàng, phiếu nhập (nháp → xác nhận tạo lô/serial), phiếu xuất (FEFO),
/// lô hàng & cảnh báo tồn, sổ kho, tài sản cá thể, danh mục nhà cung cấp/kho/đơn vị/nhóm hàng.
/// Tồn kho luôn tính từ sổ kho; chứng từ đã xác nhận không sửa được.
/// </summary>
public sealed class InventoryPage : ModulePage
{
    private const string ActiveItemsUrl = "v1/app/inventory-items?status=HOAT_DONG";
    private static readonly string[] ItemTypes = ["THUC_PHAM", "DO_UONG", "HANG_MINIBAR", "VAT_TU", "DO_VAI", "TAI_SAN", "KHAC"];
    private static readonly string[] WarehouseTypes = ["KHO_CHINH", "KHO_THUC_PHAM", "KHO_MAT", "KHO_DONG", "KHO_DO_UONG", "KHO_BUONG_PHONG", "KHO_TAI_SAN"];
    private static readonly string[] Purposes = ["BUONG_PHONG", "MINIBAR", "DICH_VU", "BEP", "SUA_CHUA", "KHAC"];
    private static readonly Dictionary<string, string> PurposeLabels = new()
    {
        ["BUONG_PHONG"] = "Buồng phòng", ["MINIBAR"] = "Minibar", ["DICH_VU"] = "Dịch vụ", ["BEP"] = "Bếp", ["SUA_CHUA"] = "Sửa chữa", ["KHAC"] = "Khác"
    };
    private static readonly Dictionary<string, string> WarehouseTypeLabels = new()
    {
        ["KHO_CHINH"] = "Kho chính", ["KHO_THUC_PHAM"] = "Kho thực phẩm", ["KHO_MAT"] = "Kho mát", ["KHO_DONG"] = "Kho đông",
        ["KHO_DO_UONG"] = "Kho đồ uống", ["KHO_BUONG_PHONG"] = "Kho buồng phòng", ["KHO_TAI_SAN"] = "Kho tài sản"
    };

    private readonly Lookups lookups;
    private readonly ComboBox lotWarehouse = Inputs.Combo([]);
    private readonly ComboBox ledgerWarehouse = Inputs.Combo([]);
    private Dictionary<long, string> warehouseNames = [];
    private Dictionary<long, string> supplierNames = [];
    private Dictionary<long, string> itemNames = [];

    public InventoryPage(ApiClient api, UserSession session) : base(api, session)
    {
        lookups = new Lookups(api);
        AddTab("Mặt hàng & tồn", Items());
        AddTab("Phiếu nhập", Purchases());
        AddTab("Phiếu xuất", Issues());
        AddTab("Lô hàng", Lots());
        AddTab("Sổ kho", Ledger());
        AddTab("Tài sản", Assets());
        AddTab("Danh mục", new CatalogPanel(this));
        BuildTabs();
    }

    public override string Title => "Kho & tài sản";

    private IWin32Window? Owner => FindForm();

    /// <summary>Nạp tên kho / nhà cung cấp / mặt hàng để hiển thị (các API chứng từ chỉ trả id).</summary>
    private async Task LoadNamesAsync()
    {
        warehouseNames = await lookups.NamesAsync(Lookups.WarehousesUrl, "ten_kho");
        supplierNames = await lookups.NamesAsync(Lookups.SuppliersUrl, "ten_nha_cung_cap");
        itemNames = (await lookups.ListAsync("v1/app/inventory-items?pageSize=100")).ToDictionary(e => J.Long(e, "id"), e => $"{J.Str(e, "code")} · {J.Str(e, "name")}");
    }

    private void InvalidateCatalog()
    {
        lookups.Invalidate();
    }

    private async Task FillWarehouseFilterAsync(ComboBox combo, bool allowAll)
    {
        if (combo.Items.Count > 0) return;
        if (allowAll) combo.Items.Add(new Choice<long?>("Tất cả kho", null));
        foreach (var (text, value) in await lookups.WarehousesAsync(false)) combo.Items.Add(new Choice<long?>(text, value));
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static long? Selected(ComboBox combo) => (combo.SelectedItem as Choice<long?>)?.Value;

    private async Task<List<(string Text, long Value)>> ActiveItemsAsync()
        => (await lookups.ListAsync(ActiveItemsUrl)).Select(e => ($"{J.Str(e, "code")} · {J.Str(e, "name")} ({J.Str(e, "unit_name")})", J.Long(e, "id"))).ToList();

    // =====================================================================
    // Mặt hàng
    // =====================================================================
    private DataView Items()
    {
        var view = new DataView(Session, "Danh mục hàng hóa và tồn kho (tất cả kho)")
        {
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/inventory-items", ("status", q.Status)))
        };
        view.AddSearch("Mã, tên hàng…")
            .AddStatusFilter("HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG")
            .Columns(
                Col.Text("Mã hàng", "code", 65),
                Col.Text("Tên hàng", "name", 140),
                Col.Label("Loại", "type", 75),
                Col.Text("Nhóm", "group_name", 75),
                Col.Text("ĐVT", "unit_name", 45),
                Col.Qty("Tồn", "balance", 55),
                Col.Qty("Tối thiểu", "minimum_stock", 55),
                Col.Custom("Cảnh báo", e => J.Bool(e, "below_minimum") ? "⚠ Dưới mức" : "", 65),
                Col.Money("Giá vốn TC", "reference_cost", 65),
                Col.Custom("Theo dõi", e => string.Join(", ", new[] { J.Bool(e, "track_lot") ? "Lô" : null, J.Bool(e, "track_expiry") ? "HSD" : null, J.Bool(e, "track_individual") ? "Cá thể" : null }.Where(x => x is not null)), 70),
                Col.Status("Trạng thái", "status", 70))
            .AddAction("Điều chỉnh tồn", r => AdjustAsync(r!.Value), "STOCK_ADJUST")
            .AddAction("Ngừng / dùng lại", r => ToggleItemAsync(r!.Value), "INVENTORY_WRITE")
            .AddAction("Sửa", r => EditItemAsync(r!.Value), "INVENTORY_WRITE")
            .AddAction("Thêm mặt hàng", _ => EditItemAsync(null), "INVENTORY_WRITE", needsRow: false, primary: true);
        if (Session.HasPermission("INVENTORY_WRITE")) view.DefaultAction = r => EditItemAsync(r!.Value);
        return view;
    }

    private async Task<bool> EditItemAsync(JsonElement? row)
    {
        var units = await lookups.UnitsAsync();
        var groups = await lookups.GroupsAsync();
        if (units.Count == 0) { Ui.Error(Owner, "Chưa có đơn vị tính. Thêm ở tab Danh mục."); return false; }
        var dlg = new FormDialog(row is null ? "Thêm mặt hàng" : "Sửa mặt hàng", 620,
            "Hàng có hạn sử dụng bắt buộc theo dõi lô; hàng theo dõi cá thể (serial) phải là Tài sản.");
        dlg.TextField("code", "Mã hàng", FormDialog.S(row, "code"), required: true);
        dlg.TextField("name", "Tên hàng", FormDialog.S(row, "name"), required: true);
        dlg.Combo("type", "Loại hàng", Labels.Options(ItemTypes), FormDialog.S(row, "type") ?? "VAT_TU");
        dlg.Combo("unit_id", "Đơn vị tính", units, FormDialog.L(row, "unit_id") ?? units[0].Value);
        dlg.Combo("group_id", "Nhóm hàng", groups, FormDialog.L(row, "group_id"), allowEmpty: true);
        dlg.Money("reference_cost", "Giá vốn tham chiếu", FormDialog.D(row, "reference_cost"));
        dlg.Number("minimum_stock", "Mức tồn tối thiểu", FormDialog.D(row, "minimum_stock"), 0, 1_000_000, 3);
        dlg.TextField("storage_condition", "Điều kiện bảo quản", FormDialog.S(row, "storage_condition"));
        dlg.Check("track_lot", "Theo dõi lô", FormDialog.B(row, "track_lot"));
        dlg.Check("track_expiry", "Theo dõi hạn sử dụng", FormDialog.B(row, "track_expiry"));
        dlg.Check("track_individual", "Theo dõi từng cá thể (serial)", FormDialog.B(row, "track_individual"));
        dlg.TextField("description", "Mô tả", FormDialog.S(row, "description"), span: 2, multiline: true);
        dlg.Validator = v =>
        {
            if ((bool)v["track_expiry"]! && !(bool)v["track_lot"]!) return "Hàng có hạn sử dụng phải theo dõi lô.";
            if ((bool)v["track_individual"]! && (string?)v["type"] != "TAI_SAN") return "Chỉ hàng loại Tài sản mới theo dõi cá thể.";
            return null;
        };
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/inventory-items/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/inventory-items", v);
        if (!dlg.ShowFor(Owner)) return false;
        InvalidateCatalog();
        return true;
    }

    private async Task<bool> ToggleItemAsync(JsonElement row)
    {
        var status = J.Str(row, "status") == "HOAT_DONG" ? "NGUNG_HOAT_DONG" : "HOAT_DONG";
        if (!Ui.Confirm(Owner, status == "HOAT_DONG" ? $"Dùng lại mặt hàng {J.Str(row, "name")}?" : $"Ngừng dùng mặt hàng {J.Str(row, "name")}? Hàng ngừng dùng không lập phiếu mới được.")) return false;
        await Api.PatchJsonAsync($"v1/inventory-items/{J.Long(row, "id")}/status", new { status });
        InvalidateCatalog();
        return true;
    }

    private async Task<bool> AdjustAsync(JsonElement row)
    {
        await LoadNamesAsync();
        var warehouses = await lookups.WarehousesAsync();
        if (warehouses.Count == 0) { Ui.Error(Owner, "Chưa có kho."); return false; }
        var lots = J.Items(await Api.GetJsonAsync(ApiClient.Url("v1/inventory-lots", ("itemId", J.Long(row, "id")), ("pageSize", 100))))
            .Where(l => J.Dec(l, "balance") != 0 || J.Str(l, "trang_thai") != "HET_HAN")
            .Select(l => ($"{J.Str(l, "ma_lo")} · {warehouseNames.GetValueOrDefault(J.Long(l, "kho_id"), "kho #" + J.Long(l, "kho_id"))} · tồn {J.Qty(J.Dec(l, "balance"))}" +
                (J.Day(l, "han_su_dung") is { } hsd ? $" · HSD {hsd:dd/MM/yyyy}" : ""), J.Long(l, "id"))).ToList();
        var dlg = new FormDialog($"Điều chỉnh tồn – {J.Str(row, "name")}", 600,
            "Số âm = giảm tồn. Hỏng/mất và thanh lý bắt buộc là số âm. Hàng theo dõi lô phải chọn đúng lô.");
        dlg.Combo("warehouse_id", "Kho", warehouses, warehouses[0].Value);
        dlg.Combo("lot_id", "Lô hàng", lots, null, allowEmpty: true, emptyText: J.Bool(row, "track_lot") ? "(Chọn lô)" : "(Không theo lô)");
        dlg.Combo("type", "Loại điều chỉnh", new (string Text, string Value)[] { ("Điều chỉnh kiểm kê", "DIEU_CHINH"), ("Hỏng / mất", "HONG_MAT"), ("Thanh lý", "THANH_LY") }, "DIEU_CHINH");
        dlg.Number("quantity_change", $"Số lượng thay đổi ({J.Str(row, "unit_name")})", 0, -1_000_000, 1_000_000, 3);
        dlg.TextField("reason", "Lý do", null, required: true, span: 2, multiline: true);
        dlg.Fixed("item_id", J.Long(row, "id"));
        dlg.Validator = v =>
        {
            var q = (decimal)v["quantity_change"]!;
            if (q == 0) return "Số lượng thay đổi phải khác 0.";
            if ((string?)v["type"] != "DIEU_CHINH" && q > 0) return "Hỏng/mất và thanh lý phải là số âm.";
            if (J.Bool(row, "track_lot") && v["lot_id"] is null) return "Mặt hàng theo dõi lô: chọn lô cần điều chỉnh.";
            return null;
        };
        dlg.OnSubmit = v => Api.PostJsonAsync("v1/stock-adjustments", v);
        return dlg.ShowFor(Owner);
    }

    // =====================================================================
    // Phiếu nhập
    // =====================================================================
    private DataView Purchases()
    {
        var view = new DataView(Session, "Phiếu nhập hàng")
        {
            Paged = true,
            Loader = async q =>
            {
                await LoadNamesAsync();
                return await Api.GetJsonAsync(ApiClient.Url("v1/purchase-orders", ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search), ("status", q.Status)));
            }
        };
        view.AddSearch("Mã phiếu nhập…", 180)
            .AddStatusFilter(null, "NHAP", "DA_XAC_NHAN", "DA_HUY")
            .Columns(
                Col.Custom("Mã phiếu", e => Short(J.Str(e, "ma_phieu_nhap")), 80),
                Col.Custom("Nhà cung cấp", e => supplierNames.GetValueOrDefault(J.Long(e, "nha_cung_cap_id")), 120),
                Col.Custom("Kho", e => warehouseNames.GetValueOrDefault(J.Long(e, "kho_id")), 90),
                Col.Time("Ngày nhập", "ngay_nhap", 80),
                Col.Text("Số HĐ NCC", "so_hoa_don_ncc", 70),
                Col.Money("Tiền hàng", "tong_tien_hang", 70),
                Col.Money("Thanh toán", "tong_thanh_toan", 75),
                Col.Text("Ghi chú", "ghi_chu", 110),
                Col.Status("Trạng thái", "trang_thai", 80))
            .AddAction("Xác nhận nhập kho", r => ConfirmPurchaseAsync(r!.Value), "PURCHASE_CONFIRM", primary: true, when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Hủy phiếu", r => CancelDocumentAsync(r!.Value, "purchase-orders"), "PURCHASE_WRITE", when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Sửa", r => EditPurchaseAsync(r!.Value), "PURCHASE_WRITE", when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Xem", r => ShowPurchaseAsync(r!.Value))
            .AddAction("Lập phiếu nhập", _ => EditPurchaseAsync(null), "PURCHASE_WRITE", needsRow: false);
        view.DefaultAction = r => ShowPurchaseAsync(r!.Value);
        return view;
    }

    private async Task<bool> EditPurchaseAsync(JsonElement? row)
    {
        var suppliers = await lookups.SuppliersAsync();
        var warehouses = await lookups.WarehousesAsync();
        var items = await ActiveItemsAsync();
        if (suppliers.Count == 0 || warehouses.Count == 0 || items.Count == 0)
        {
            Ui.Error(Owner, "Cần có ít nhất một nhà cung cấp, một kho và một mặt hàng đang hoạt động (tab Danh mục / Mặt hàng).");
            return false;
        }
        JsonElement? detail = row is { } r0 ? await Api.GetJsonAsync($"v1/purchase-orders/{J.Long(r0, "id")}") : null;
        var dlg = new FormDialog(row is null ? "Lập phiếu nhập" : $"Sửa phiếu nhập {Short(J.Str(row.Value, "ma_phieu_nhap"))}", 760,
            "Phiếu được lưu nháp; lô, hạn dùng và serial nhập khi xác nhận nhập kho.");
        dlg.Combo("supplier_id", "Nhà cung cấp", suppliers, FormDialog.L(detail, "nha_cung_cap_id") ?? suppliers[0].Value);
        dlg.Combo("warehouse_id", "Kho nhận", warehouses, FormDialog.L(detail, "kho_id") ?? warehouses[0].Value);
        dlg.Money("discount", "Chiết khấu", FormDialog.D(detail, "chiet_khau"));
        dlg.Money("tax", "Tiền thuế", FormDialog.D(detail, "tien_thue"));
        if (row is not null)
        {
            dlg.TextField("supplier_invoice_number", "Số hóa đơn NCC", FormDialog.S(detail, "so_hoa_don_ncc"));
            dlg.Date("supplier_invoice_date", "Ngày hóa đơn NCC", FormDialog.Day(detail, "ngay_hoa_don_ncc"), nullable: true);
        }
        var lines = new LinesEditor(200, true,
            new LineCol("item_id", "Mặt hàng", LineKind.Combo, 200, items.Select(i => (i.Text, (object)i.Value)).ToList()),
            new LineCol("quantity", "Số lượng", LineKind.Number, 60),
            new LineCol("unit_price", "Đơn giá nhập", LineKind.Number, 70));
        if (detail is { } d)
            foreach (var l in J.Items(d, "chi_tiet"))
                lines.AddRow(new Dictionary<string, object?> { ["item_id"] = J.Long(l, "danh_muc_hang_hoa_id"), ["quantity"] = J.Dec(l, "so_luong"), ["unit_price"] = J.Dec(l, "don_gia_nhap") });
        dlg.Custom("lines", "Dòng hàng", lines, () => lines.Lines());
        dlg.TextField("note", "Ghi chú", FormDialog.S(detail, "ghi_chu"), span: 2);
        dlg.Fixed("employee_id", 0);
        dlg.Validator = v =>
        {
            var l = (List<Dictionary<string, object?>>)v["lines"]!;
            if (l.Count == 0) return "Thêm ít nhất một dòng hàng.";
            if (l.Any(x => x["item_id"] is null || x["quantity"] is not decimal q || q <= 0 || x["unit_price"] is not decimal p || p < 0))
                return "Mỗi dòng cần mặt hàng, số lượng > 0 và đơn giá ≥ 0.";
            return null;
        };
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/purchase-orders/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/purchase-orders", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> ShowPurchaseAsync(JsonElement row)
    {
        var detail = await Api.GetJsonAsync($"v1/purchase-orders/{J.Long(row, "id")}");
        using var dlg = new DetailDialog($"Phiếu nhập {Short(J.Str(row, "ma_phieu_nhap"))}", Labels.Of(J.Str(row, "trang_thai")));
        dlg.Info(("Nhà cung cấp", supplierNames.GetValueOrDefault(J.Long(detail, "nha_cung_cap_id"))), ("Kho", warehouseNames.GetValueOrDefault(J.Long(detail, "kho_id"))),
            ("Ngày nhập", J.TimeText(J.Time(detail, "ngay_nhap"))), ("Xác nhận lúc", J.TimeText(J.Time(detail, "xac_nhan_luc"))),
            ("Số HĐ NCC", J.StrOrNull(detail, "so_hoa_don_ncc")), ("Ngày HĐ NCC", J.DateText(J.Day(detail, "ngay_hoa_don_ncc"))),
            ("Tiền hàng", J.MoneyVnd(J.Dec(detail, "tong_tien_hang"))), ("Chiết khấu", J.MoneyVnd(J.Dec(detail, "chiet_khau"))),
            ("Thuế", J.MoneyVnd(J.Dec(detail, "tien_thue"))), ("Tổng thanh toán", J.MoneyVnd(J.Dec(detail, "tong_thanh_toan"))),
            ("Ghi chú", J.StrOrNull(detail, "ghi_chu")));
        dlg.Table("Dòng hàng", J.Items(detail, "chi_tiet"), 220,
            Col.Custom("Mặt hàng", e => itemNames.GetValueOrDefault(J.Long(e, "danh_muc_hang_hoa_id"), "#" + J.Long(e, "danh_muc_hang_hoa_id")), 180),
            Col.Qty("Số lượng", "so_luong", 60), Col.Money("Đơn giá", "don_gia_nhap", 70), Col.Money("Thành tiền", "thanh_tien", 80));
        dlg.ShowDialog(Owner);
        return false;
    }

    private async Task<bool> ConfirmPurchaseAsync(JsonElement row)
    {
        var detail = await Api.GetJsonAsync($"v1/purchase-orders/{J.Long(row, "id")}");
        var catalog = (await lookups.ListAsync(Lookups.ItemsUrl)).ToDictionary(e => J.Long(e, "id"));
        var lines = J.Items(detail, "chi_tiet");
        var editor = new LinesEditor(Math.Min(320, 60 + lines.Count * 32), false,
            new LineCol("item", "Mặt hàng", LineKind.ReadOnly, 150),
            new LineCol("qty", "SL", LineKind.ReadOnly, 40),
            new LineCol("need", "Yêu cầu", LineKind.ReadOnly, 70),
            new LineCol("lot_code", "Mã lô", LineKind.Text, 70),
            new LineCol("manufacture_date", "NSX", LineKind.Date, 75),
            new LineCol("expiry_date", "HSD", LineKind.Date, 75),
            new LineCol("serials", "Số sê-ri (ngăn bằng dấu phẩy)", LineKind.Text, 140)) { SkipEmptyRows = false };
        foreach (var l in lines)
        {
            var item = catalog.GetValueOrDefault(J.Long(l, "danh_muc_hang_hoa_id"));
            var need = string.Join(", ", new[] { J.Bool(item, "track_lot") ? "lô" : null, J.Bool(item, "track_expiry") ? "HSD" : null, J.Bool(item, "track_individual") ? "serial" : null }.Where(x => x is not null));
            editor.AddRow(new Dictionary<string, object?>
            {
                ["item"] = $"{J.Str(item, "code")} · {J.Str(item, "name")}", ["qty"] = J.Qty(J.Dec(l, "so_luong")), ["need"] = need.Length == 0 ? "—" : need
            });
        }
        var dlg = new FormDialog($"Xác nhận nhập kho – phiếu {Short(J.Str(row, "ma_phieu_nhap"))}", 900,
            $"{supplierNames.GetValueOrDefault(J.Long(detail, "nha_cung_cap_id"))} → {warehouseNames.GetValueOrDefault(J.Long(detail, "kho_id"))} · {J.MoneyVnd(J.Dec(detail, "tong_thanh_toan"))}",
            columns: 1, okText: "Xác nhận nhập kho");
        dlg.Note("Sau khi xác nhận: tạo lô, tài sản cá thể và cộng tồn kho. Phiếu đã xác nhận không sửa được. Hàng theo HSD bắt buộc mã lô + HSD; hàng theo serial nhập đủ số serial bằng số lượng (hoặc để trống để hệ thống tự tạo mã tài sản).");
        dlg.Custom("lines", "", editor, () =>
        {
            var rows = editor.Lines();
            return rows.Select((r, i) => new
            {
                detail_id = J.Long(lines[i], "id"),
                lot_code = (string?)r["lot_code"],
                manufacture_date = (string?)r["manufacture_date"],
                expiry_date = (string?)r["expiry_date"],
                serials = r["serials"] is string s ? s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : null
            }).ToList();
        });
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/purchase-orders/{J.Long(row, "id")}:confirm", new { lines = v["lines"] });
        if (!dlg.ShowFor(Owner)) return false;
        InvalidateCatalog();
        return true;
    }

    private async Task<bool> CancelDocumentAsync(JsonElement row, string resource)
    {
        var reason = Ui.Prompt(Owner, "Hủy phiếu nháp", "Lý do hủy:");
        if (reason is null) return false;
        await Api.PostJsonAsync($"v1/{resource}/{J.Long(row, "id")}:cancel", new { reason });
        return true;
    }

    // =====================================================================
    // Phiếu xuất
    // =====================================================================
    private DataView Issues()
    {
        var view = new DataView(Session, "Phiếu xuất kho")
        {
            Paged = true,
            Loader = async q =>
            {
                await LoadNamesAsync();
                return await Api.GetJsonAsync(ApiClient.Url("v1/stock-issues", ("page", q.Page), ("pageSize", q.PageSize), ("status", q.Status)));
            }
        };
        view.AddStatusFilter(null, "NHAP", "DA_XAC_NHAN", "DA_HUY")
            .Columns(
                Col.Custom("Mã phiếu", e => Short(J.Str(e, "ma_phieu_xuat")), 80),
                Col.Custom("Kho", e => warehouseNames.GetValueOrDefault(J.Long(e, "kho_id")), 100),
                Col.Custom("Mục đích", e => PurposeLabels.GetValueOrDefault(J.Str(e, "muc_dich"), J.Str(e, "muc_dich")), 75),
                Col.Text("Người nhận", "nguoi_nhan", 90),
                Col.Text("Lý do", "ly_do", 140),
                Col.Time("Ngày xuất", "ngay_xuat", 80),
                Col.Time("Xác nhận lúc", "xac_nhan_luc", 80),
                Col.Status("Trạng thái", "trang_thai", 80))
            .AddAction("Xác nhận xuất kho", r => ConfirmIssueAsync(r!.Value), "STOCK_ISSUE_CONFIRM", primary: true, when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Hủy phiếu", r => CancelDocumentAsync(r!.Value, "stock-issues"), "STOCK_ISSUE_WRITE", when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Sửa", r => EditIssueAsync(r!.Value), "STOCK_ISSUE_WRITE", when: r => J.Str(r, "trang_thai") == "NHAP")
            .AddAction("Xem", r => ShowIssueAsync(r!.Value))
            .AddAction("Lập phiếu xuất", _ => EditIssueAsync(null), "STOCK_ISSUE_WRITE", needsRow: false);
        view.DefaultAction = r => ShowIssueAsync(r!.Value);
        return view;
    }

    private async Task<bool> EditIssueAsync(JsonElement? row)
    {
        var warehouses = await lookups.WarehousesAsync();
        var items = await ActiveItemsAsync();
        if (warehouses.Count == 0 || items.Count == 0) { Ui.Error(Owner, "Cần có kho và mặt hàng đang hoạt động."); return false; }
        JsonElement? detail = row is { } r0 ? await Api.GetJsonAsync($"v1/stock-issues/{J.Long(r0, "id")}") : null;
        var dlg = new FormDialog(row is null ? "Lập phiếu xuất" : "Sửa phiếu xuất", 700,
            "Khi xác nhận, hệ thống xuất theo lô hết hạn trước (FEFO) và kiểm tra đủ tồn.");
        dlg.Combo("warehouse_id", "Kho xuất", warehouses, FormDialog.L(detail, "kho_id") ?? warehouses[0].Value);
        dlg.Combo("purpose", "Mục đích", Purposes.Select(p => (PurposeLabels[p], p)), FormDialog.S(detail, "muc_dich") ?? "BUONG_PHONG");
        dlg.TextField("recipient", "Người / bộ phận nhận", FormDialog.S(detail, "nguoi_nhan"));
        dlg.TextField("reason", "Lý do", FormDialog.S(detail, "ly_do"));
        var lines = new LinesEditor(200, true,
            new LineCol("item_id", "Mặt hàng", LineKind.Combo, 220, items.Select(i => (i.Text, (object)i.Value)).ToList()),
            new LineCol("quantity", "Số lượng", LineKind.Number, 70));
        if (detail is { } d)
            foreach (var l in J.Items(d, "chi_tiet"))
                lines.AddRow(new Dictionary<string, object?> { ["item_id"] = J.Long(l, "danh_muc_hang_hoa_id"), ["quantity"] = J.Dec(l, "so_luong") });
        dlg.Custom("lines", "Dòng hàng xuất", lines, () => lines.Lines());
        dlg.Fixed("employee_id", 0);
        dlg.Validator = v =>
        {
            var l = (List<Dictionary<string, object?>>)v["lines"]!;
            if (l.Count == 0) return "Thêm ít nhất một dòng hàng.";
            if (l.Any(x => x["item_id"] is null || x["quantity"] is not decimal q || q <= 0)) return "Mỗi dòng cần mặt hàng và số lượng > 0.";
            return null;
        };
        dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/stock-issues/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/stock-issues", v);
        return dlg.ShowFor(Owner);
    }

    private async Task<bool> ShowIssueAsync(JsonElement row)
    {
        var detail = await Api.GetJsonAsync($"v1/stock-issues/{J.Long(row, "id")}");
        using var dlg = new DetailDialog($"Phiếu xuất {Short(J.Str(row, "ma_phieu_xuat"))}", Labels.Of(J.Str(row, "trang_thai")), 720, 520);
        dlg.Info(("Kho", warehouseNames.GetValueOrDefault(J.Long(detail, "kho_id"))), ("Mục đích", PurposeLabels.GetValueOrDefault(J.Str(detail, "muc_dich"))),
            ("Người nhận", J.StrOrNull(detail, "nguoi_nhan")), ("Lý do", J.StrOrNull(detail, "ly_do")),
            ("Ngày xuất", J.TimeText(J.Time(detail, "ngay_xuat"))), ("Xác nhận", J.TimeText(J.Time(detail, "xac_nhan_luc"))));
        dlg.Table("Dòng hàng", J.Items(detail, "chi_tiet"), 220,
            Col.Custom("Mặt hàng", e => itemNames.GetValueOrDefault(J.Long(e, "danh_muc_hang_hoa_id"), "#" + J.Long(e, "danh_muc_hang_hoa_id")), 200),
            Col.Qty("Số lượng", "so_luong", 70), Col.Text("Ghi chú", "ghi_chu", 120));
        dlg.ShowDialog(Owner);
        return false;
    }

    private async Task<bool> ConfirmIssueAsync(JsonElement row)
    {
        if (!Ui.Confirm(Owner, "Xác nhận xuất kho? Tồn kho sẽ bị trừ theo lô hết hạn trước; phiếu không sửa được sau khi xác nhận.")) return false;
        await Api.PostJsonAsync($"v1/stock-issues/{J.Long(row, "id")}:confirm");
        InvalidateCatalog();
        return true;
    }

    // =====================================================================
    // Lô hàng, sổ kho
    // =====================================================================
    private DataView Lots()
    {
        var expiring = new CheckBox { Text = "Hết hạn trong 30 ngày", AutoSize = true, Font = AppTheme.Body };
        var view = new DataView(Session, "Lô hàng & hạn sử dụng")
        {
            Paged = true,
            Loader = async q =>
            {
                await FillWarehouseFilterAsync(lotWarehouse, true);
                await LoadNamesAsync();
                return await Api.GetJsonAsync(ApiClient.Url("v1/inventory-lots", ("page", q.Page), ("pageSize", q.PageSize),
                    ("warehouseId", Selected(lotWarehouse)), ("expiresBefore", expiring.Checked ? (object?)DateOnly.FromDateTime(DateTime.Today.AddDays(30)) : null)));
            }
        };
        lotWarehouse.Width = 180;
        view.AddFilter(lotWarehouse);
        view.AddFilter(expiring);
        view.AddApplyButton();
        view.Columns(
                Col.Text("Mã lô", "ma_lo", 90),
                Col.Custom("Mặt hàng", e => itemNames.GetValueOrDefault(J.Long(e, "danh_muc_hang_hoa_id")), 160),
                Col.Custom("Kho", e => warehouseNames.GetValueOrDefault(J.Long(e, "kho_id")), 100),
                Col.Day("NSX", "ngay_san_xuat", 70),
                Col.Day("Hạn sử dụng", "han_su_dung", 75),
                Col.Custom("Còn", e => J.Day(e, "han_su_dung") is { } d ? $"{(d - DateTime.Today).Days} ngày" : "", 60),
                Col.Qty("Tồn lô", "balance", 60),
                Col.Status("Trạng thái", "trang_thai", 80))
            .AddAction("Cảnh báo tồn thấp", _ => ShowAlertsAsync(), needsRow: false);
        return view;
    }

    private async Task<bool> ShowAlertsAsync()
    {
        if (Selected(lotWarehouse) is not { } warehouseId) { Ui.Info(Owner, "Chọn một kho cụ thể ở bộ lọc để xem cảnh báo."); return false; }
        var rows = J.Items(await Api.GetJsonAsync($"v1/inventory-alerts?warehouseId={warehouseId}"));
        using var dlg = new DetailDialog("Mặt hàng dưới mức tồn tối thiểu", warehouseNames.GetValueOrDefault(warehouseId), 620, 480);
        dlg.Table($"{rows.Count} mặt hàng cần nhập thêm", rows, 300,
            Col.Text("Mặt hàng", "ten_hang", 180), Col.Qty("Tồn", "balance", 60), Col.Qty("Tối thiểu", "minimum", 60));
        dlg.ShowDialog(Owner);
        return false;
    }

    private DataView Ledger()
    {
        var view = new DataView(Session, "Sổ kho (mọi biến động nhập, xuất, điều chỉnh)")
        {
            Paged = true,
            PageSize = 50,
            Loader = async q =>
            {
                await FillWarehouseFilterAsync(ledgerWarehouse, true);
                await LoadNamesAsync();
                return await Api.GetJsonAsync(ApiClient.Url("v1/stock-ledger", ("page", q.Page), ("pageSize", q.PageSize), ("warehouseId", Selected(ledgerWarehouse))));
            }
        };
        ledgerWarehouse.Width = 180;
        view.AddFilter(ledgerWarehouse);
        view.AddApplyButton();
        view.Columns(
                Col.Time("Thời gian", "thoi_gian", 80),
                Col.Custom("Kho", e => warehouseNames.GetValueOrDefault(J.Long(e, "kho_id")), 90),
                Col.Custom("Mặt hàng", e => itemNames.GetValueOrDefault(J.Long(e, "danh_muc_hang_hoa_id")), 160),
                Col.Label("Loại", "loai_giao_dich", 75),
                Col.Custom("Thay đổi", e => (J.Dec(e, "so_luong_thay_doi") > 0 ? "+" : "") + J.Qty(J.Dec(e, "so_luong_thay_doi")), 60),
                Col.Text("Tham chiếu", "tham_chieu", 100),
                Col.Text("Ghi chú", "ghi_chu", 130));
        return view;
    }

    // =====================================================================
    // Tài sản
    // =====================================================================
    private DataView Assets()
    {
        var view = new DataView(Session, "Tài sản cá thể")
        {
            Paged = true,
            Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/app/assets", ("page", q.Page), ("pageSize", q.PageSize), ("search", q.Search), ("status", q.Status)))
        };
        view.AddSearch("Mã tài sản, tên, serial…")
            .AddStatusFilter(null, "TOT", "HONG", "BAO_TRI", "MAT", "THANH_LY")
            .Columns(
                Col.Custom("Mã tài sản", e => Short(J.Str(e, "asset_code")), 80),
                Col.Text("Tên", "item_name", 140),
                Col.Text("Serial", "serial", 90),
                Col.Text("Vị trí", "location", 110),
                Col.Money("Giá mua", "purchase_price", 70),
                Col.Day("Ngày mua", "purchase_date", 70),
                Col.Text("Ghi chú", "note", 120),
                Col.Status("Tình trạng", "condition", 75))
            .AddAction("Cập nhật tình trạng", r => AssetConditionAsync(r!.Value), "ASSET_WRITE")
            .AddAction("Chuyển vị trí", r => TransferAssetAsync(r!.Value), "ASSET_WRITE", primary: true,
                when: r => J.Str(r, "condition") is not ("MAT" or "THANH_LY"));
        return view;
    }

    private async Task<bool> TransferAssetAsync(JsonElement row)
    {
        var rooms = (await lookups.ListAsync("v1/app/rooms")).Select(e => ($"Phòng {J.Str(e, "room_number")}", J.Long(e, "id"))).ToList();
        var warehouses = await lookups.WarehousesAsync();
        var dlg = new FormDialog($"Chuyển tài sản {J.Str(row, "item_name")}", 560, $"Hiện ở: {J.Str(row, "location")}. Chọn đúng một nơi đến.");
        dlg.Combo("room_id", "Đến phòng", rooms, null, allowEmpty: true, emptyText: "(Không)");
        dlg.Combo("warehouse_id", "Hoặc về kho", warehouses, null, allowEmpty: true, emptyText: "(Không)");
        dlg.TextField("other_location", "Hoặc vị trí khác (sảnh, nhà hàng…)", null, span: 2);
        dlg.TextField("reason", "Lý do", null, required: true, span: 2);
        dlg.Validator = v => (v["room_id"] is null ? 0 : 1) + (v["warehouse_id"] is null ? 0 : 1) + (v["other_location"] is null ? 0 : 1) != 1
            ? "Chọn đúng một nơi đến: phòng, kho hoặc vị trí khác." : null;
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/assets/{J.Long(row, "id")}:transfer", v);
        return dlg.ShowFor(Owner);
    }

    private Task<bool> AssetConditionAsync(JsonElement row)
    {
        var dlg = new FormDialog($"Tình trạng tài sản {J.Str(row, "item_name")}", 480, J.Str(row, "location"), columns: 1);
        dlg.Codes("condition", "Tình trạng mới", J.StrOrNull(row, "condition"), "TOT", "HONG", "BAO_TRI", "MAT", "THANH_LY");
        dlg.TextField("reason", "Lý do / mô tả", null, required: true, multiline: true);
        dlg.OnSubmit = v => Api.PostJsonAsync($"v1/assets/{J.Long(row, "id")}:condition", v);
        return Task.FromResult(dlg.ShowFor(Owner));
    }

    private static string Short(string code) => code.Length > 14 ? code[..3] + "…" + code[^6..] : code;

    // =====================================================================
    // Danh mục: nhà cung cấp, kho, đơn vị, nhóm hàng (4 bảng trên một tab)
    // =====================================================================
    private sealed class CatalogPanel : UserControl, IReloadable
    {
        private readonly InventoryPage page;
        private readonly List<DataView> views = [];

        public CatalogPanel(InventoryPage page)
        {
            this.page = page;
            Dock = DockStyle.Fill;
            BackColor = AppTheme.Background;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = AppTheme.Background };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.Controls.Add(Wrap(Suppliers()), 0, 0);
            layout.Controls.Add(Wrap(Warehouses()), 1, 0);
            layout.Controls.Add(Wrap(Lookup("Đơn vị tính", Lookups.UnitsUrl, "inventory-units", "ma_don_vi", "ten_don_vi")), 0, 1);
            layout.Controls.Add(Wrap(Lookup("Nhóm hàng", Lookups.GroupsUrl, "inventory-groups", "ma_nhom", "ten_nhom")), 1, 1);
            Controls.Add(layout);
        }

        private Control Wrap(DataView view)
        {
            views.Add(view);
            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 10), BackColor = AppTheme.Background };
            holder.Controls.Add(view);
            return holder;
        }

        public async Task ReloadAsync()
        {
            foreach (var v in views) await v.ReloadAsync();
        }

        private ApiClient Api => page.Api;
        private UserSession Session => page.Session;
        private IWin32Window? Owner => FindForm();

        private DataView Suppliers()
        {
            var view = new DataView(Session, "Nhà cung cấp")
            {
                Loader = q => Api.GetJsonAsync(ApiClient.Url("v1/suppliers", ("pageSize", 100), ("status", q.Status)))
            };
            view.AddSearch("Tên, mã…", 150)
                .Columns(Col.Text("Mã", "ma_nha_cung_cap", 50), Col.Text("Tên", "ten_nha_cung_cap", 120), Col.Text("Liên hệ", "nguoi_lien_he", 70),
                    Col.Text("Điện thoại", "so_dien_thoai", 70), Col.Status("TT", "trang_thai", 60))
                .AddAction("Sửa", r => EditSupplierAsync(r!.Value), "INVENTORY_WRITE")
                .AddAction("Thêm", _ => EditSupplierAsync(null), "INVENTORY_WRITE", needsRow: false, primary: true);
            return view;
        }

        private Task<bool> EditSupplierAsync(JsonElement? row)
        {
            var dlg = new FormDialog(row is null ? "Thêm nhà cung cấp" : "Sửa nhà cung cấp", 600);
            dlg.TextField("code", "Mã", FormDialog.S(row, "ma_nha_cung_cap"), required: true);
            dlg.TextField("name", "Tên nhà cung cấp", FormDialog.S(row, "ten_nha_cung_cap"), required: true);
            dlg.TextField("contact", "Người liên hệ", FormDialog.S(row, "nguoi_lien_he"));
            dlg.TextField("phone", "Điện thoại", FormDialog.S(row, "so_dien_thoai"));
            dlg.TextField("email", "Email", FormDialog.S(row, "email"));
            dlg.TextField("tax_code", "Mã số thuế", FormDialog.S(row, "ma_so_thue"));
            dlg.TextField("address", "Địa chỉ", FormDialog.S(row, "dia_chi"), span: 2);
            dlg.Codes("status", "Trạng thái", FormDialog.S(row, "trang_thai") ?? "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
            dlg.TextField("note", "Ghi chú", FormDialog.S(row, "ghi_chu"));
            dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/suppliers/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/suppliers", v);
            var ok = dlg.ShowFor(Owner);
            if (ok) page.InvalidateCatalog();
            return Task.FromResult(ok);
        }

        private DataView Warehouses()
        {
            var view = new DataView(Session, "Kho") { Loader = _ => Api.GetJsonAsync(Lookups.WarehousesUrl) };
            view.Columns(Col.Text("Mã", "ma_kho", 50), Col.Text("Tên kho", "ten_kho", 110),
                    Col.Custom("Loại", e => WarehouseTypeLabels.GetValueOrDefault(J.Str(e, "loai_kho"), J.Str(e, "loai_kho")), 80),
                    Col.Text("Vị trí", "vi_tri", 80), Col.Status("TT", "trang_thai", 60))
                .AddAction("Sửa", r => EditWarehouseAsync(r!.Value), "INVENTORY_WRITE")
                .AddAction("Thêm", _ => EditWarehouseAsync(null), "INVENTORY_WRITE", needsRow: false, primary: true);
            return view;
        }

        private Task<bool> EditWarehouseAsync(JsonElement? row)
        {
            var dlg = new FormDialog(row is null ? "Thêm kho" : "Sửa kho", 560);
            dlg.TextField("code", "Mã kho", FormDialog.S(row, "ma_kho"), required: true);
            dlg.TextField("name", "Tên kho", FormDialog.S(row, "ten_kho"), required: true);
            dlg.Combo("type", "Loại kho", WarehouseTypes.Select(t => (WarehouseTypeLabels[t], t)), FormDialog.S(row, "loai_kho") ?? "KHO_CHINH");
            dlg.TextField("location", "Vị trí", FormDialog.S(row, "vi_tri"));
            dlg.Number("minimum_temperature", "Nhiệt độ từ (°C)", FormDialog.D(row, "nhiet_do_tu"), -50, 60, 1);
            dlg.Number("maximum_temperature", "Nhiệt độ đến (°C)", FormDialog.D(row, "nhiet_do_den", 30), -50, 60, 1);
            dlg.Codes("status", "Trạng thái", FormDialog.S(row, "trang_thai") ?? "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
            dlg.Validator = v => (decimal)v["minimum_temperature"]! > (decimal)v["maximum_temperature"]! ? "Nhiệt độ từ phải nhỏ hơn nhiệt độ đến." : null;
            dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/warehouses/{J.Long(r, "id")}", v) : Api.PostJsonAsync("v1/warehouses", v);
            var ok = dlg.ShowFor(Owner);
            if (ok) page.InvalidateCatalog();
            return Task.FromResult(ok);
        }

        private DataView Lookup(string title, string url, string resource, string codePath, string namePath)
        {
            var view = new DataView(Session, title) { Loader = _ => Api.GetJsonAsync(url) };
            view.Columns(Col.Text("Mã", codePath, 60), Col.Text("Tên", namePath, 120), Col.Text("Mô tả", "mo_ta", 120), Col.Status("TT", "trang_thai", 60))
                .AddAction("Sửa", r => EditLookupAsync(title, resource, codePath, namePath, r!.Value), "INVENTORY_WRITE")
                .AddAction("Thêm", _ => EditLookupAsync(title, resource, codePath, namePath, null), "INVENTORY_WRITE", needsRow: false, primary: true);
            return view;
        }

        private Task<bool> EditLookupAsync(string title, string resource, string codePath, string namePath, JsonElement? row)
        {
            var dlg = new FormDialog((row is null ? "Thêm " : "Sửa ") + title.ToLowerInvariant(), 500);
            dlg.TextField("code", "Mã", FormDialog.S(row, codePath), required: true);
            dlg.TextField("name", "Tên", FormDialog.S(row, namePath), required: true);
            dlg.TextField("description", "Mô tả", FormDialog.S(row, "mo_ta"), span: 2);
            dlg.Codes("status", "Trạng thái", FormDialog.S(row, "trang_thai") ?? "HOAT_DONG", "HOAT_DONG", "NGUNG_HOAT_DONG");
            dlg.OnSubmit = v => row is { } r ? Api.PutJsonAsync($"v1/{resource}/{J.Long(r, "id")}", v) : Api.PostJsonAsync($"v1/{resource}", v);
            var ok = dlg.ShowFor(Owner);
            if (ok) page.InvalidateCatalog();
            return Task.FromResult(ok);
        }
    }
}
