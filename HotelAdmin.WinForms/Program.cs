using HotelAdmin.Forms;
using HotelAdmin.Services;

namespace HotelAdmin;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var config = AppConfig.Load();
        var api = new ApiClient(config);

        // Vòng lặp: đăng nhập -> mở app; khi đăng xuất hoặc hết phiên thì quay lại màn hình đăng nhập.
        while (true)
        {
            using var login = new FrmLogin(api, config);
            if (login.ShowDialog() != DialogResult.OK || login.Session is null) return;

            using var main = new FrmMain(api, login.Session, config);
            Application.Run(main);
            if (!main.LogoutRequested) return;
            api.ClearToken();
        }
    }
}
