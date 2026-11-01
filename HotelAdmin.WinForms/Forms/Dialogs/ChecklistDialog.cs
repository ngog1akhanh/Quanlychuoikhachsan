using HotelAdmin.Theme;

namespace HotelAdmin.Forms.Dialogs;

/// <summary>Hộp thoại chọn nhiều mục có nhóm (gán vai trò, phân quyền). Trả về danh sách id đã chọn.</summary>
public sealed class ChecklistDialog : DialogBase
{
    private readonly ListView list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
        Font = AppTheme.Body, BorderStyle = BorderStyle.FixedSingle
    };

    public ChecklistDialog(string title, string? subtitle, IEnumerable<(string Text, string? Detail, long Id, string? Group)> items,
        IEnumerable<long> selected, int width = 620, int height = 620) : base(title, width, height, subtitle)
    {
        var chosen = selected.ToHashSet();
        list.Columns.Add("Tên", 240);
        list.Columns.Add("Mô tả / mã", width - 320);
        var groups = new Dictionary<string, ListViewGroup>();
        foreach (var (text, detail, id, group) in items)
        {
            var item = new ListViewItem(new[] { text, detail ?? "" }) { Tag = id, Checked = chosen.Contains(id) };
            if (group is not null)
            {
                if (!groups.TryGetValue(group, out var g))
                {
                    g = new ListViewGroup(group, group);
                    groups[group] = g;
                    list.Groups.Add(g);
                }
                item.Group = g;
            }
            list.Items.Add(item);
        }
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
        var all = new LinkLabel { Text = "Chọn tất cả", AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
        var none = new LinkLabel { Text = "Bỏ chọn tất cả", AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
        all.LinkClicked += (_, _) => { foreach (ListViewItem i in list.Items) i.Checked = true; };
        none.LinkClicked += (_, _) => { foreach (ListViewItem i in list.Items) i.Checked = false; };
        tools.Controls.AddRange([all, none]);
        Body.Controls.Add(list);
        Body.Controls.Add(tools);
        AddButton("Lưu", true, async (_, _) =>
        {
            if (OnSubmit is null || await RunAsync(() => OnSubmit(SelectedIds))) CloseOk();
        });
        AddCancelButton();
    }

    public List<long> SelectedIds => list.CheckedItems.Cast<ListViewItem>().Select(i => (long)i.Tag!).ToList();

    public Func<List<long>, Task>? OnSubmit { get; set; }
}
