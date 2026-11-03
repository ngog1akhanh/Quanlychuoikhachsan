using System.Text.Json;
using HotelAdmin.Controls;
using HotelAdmin.Models;
using HotelAdmin.Services;

namespace HotelAdmin.Forms.Dialogs;

public sealed class NotificationsDialog : DialogBase
{
    public NotificationsDialog(ApiClient api, UserSession session) : base("Thông báo", 900, 600)
    {
        var view = new DataView(session, "Hộp thư thông báo")
        {
            Dock = DockStyle.Fill, Paged = true, IdPath = "notification.id",
            Loader = q => api.GetJsonAsync(ApiClient.Url("v1/notifications", ("page", q.Page), ("pageSize", q.PageSize)))
        };
        view.Columns(Col.Text("Tiêu đề", "notification.tieu_de", 170),
            Col.Text("Nội dung", "notification.noi_dung", 240), Col.Bool("Đã đọc", "read", 60));
        async Task<bool> Read(JsonElement? row)
        {
            if (row is not { } item) return false;
            using var detail = new DetailDialog(J.Str(item, "notification.tieu_de"));
            detail.Info(("Nội dung", J.Str(item, "notification.noi_dung")));
            detail.ShowDialog(this);
            await api.PostJsonAsync($"v1/notifications/{J.Long(item, "notification.id")}:read");
            return true;
        }
        view.AddAction("Xem và đánh dấu đã đọc", Read, primary: true);
        view.DefaultAction = Read;
        Body.Controls.Add(view);
        AddCancelButton("Đóng");
        Shown += async (_, _) => await view.ReloadAsync();
    }
}
