using System.Reflection;
using HotelAdmin.Forms.Dialogs;
using HotelAdmin.Models;

internal static class CheckInInputSmoke
{
    // Exercises the real dialog on STA without submitting a booking or calling an API.
    public static void Run()
    {
        var summary = new FrontDeskBookingItem(1, "UI-TEST", "BOOKED", "WEB", 1, "Test", null,
            "101", DateTime.Today, DateTime.Today.AddDays(1), 1, 1, 0, 0, 0, DateTime.Now);
        var customer = new FrontDeskCustomer(1, "TEST", "Test", null, null, null, null, 0);
        foreach (DateOnly? birth in new DateOnly?[] { null, new(2000, 2, 29), new(1890, 1, 1) })
        {
            var booking = new FrontDeskBookingDetail(summary, customer, null, null,
                [new(1, 1, "101", "Standard", "SAN_SANG", "BOOKED", 0, null, null)], [],
                [new(1, 1, "Test", birth, "", "CCCD", "123", "Việt Nam", true)], [], [], [], []);
            using var dialog = new DlgCheckIn(null!, booking) { Opacity = 0, ShowInTaskbar = false };
            dialog.Show();
            Application.DoEvents();
            var grid = (DataGridView)typeof(DlgCheckIn).GetField("guests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
            var value = grid.Rows[0].Cells["birth"].Value;
            if (birth is not null && (value is not DateTime date || DateOnly.FromDateTime(date) != birth))
                throw new InvalidOperationException("Existing birth date was not preserved.");
            if (!grid.Columns["birth"]!.ReadOnly) throw new InvalidOperationException("Birth cell must use the calendar.");
            typeof(DlgCheckIn).GetMethod("PickBirthDate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, [0]);
            Application.DoEvents();
            // Merely opening a calendar must not replace the saved date (including historical dates).
            if (!Equals(value, grid.Rows[0].Cells["birth"].Value)) throw new InvalidOperationException("Opening calendar changed the date.");
            dialog.Close();
            Application.DoEvents();
        }
        Console.WriteLine("PASS Check-in calendar: blank, leap-day and historical dates; values preserved; no API writes.");
    }
}
