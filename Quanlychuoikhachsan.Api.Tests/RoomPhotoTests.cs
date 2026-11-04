using QuanLyKhachSan.Web.Models.Room;
using QuanLyKhachSan.Web.Services;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class RoomPhotoTests
{
    [Fact]
    public void Photos_have_stable_class_covers_numeric_order_and_suite_compatibility()
    {
        var root = Path.Combine(Path.GetTempPath(), "hotel-photo-test-" + Guid.NewGuid().ToString("N"));
        var photos = Path.Combine(root, "images", "rooms", "photos");
        Directory.CreateDirectory(photos);
        try
        {
            foreach (var file in new[] { "std-10.jpg", "std-2.jpg", "std-1.jpg", "exe-1.jpg", "std-0.txt" })
                File.WriteAllText(Path.Combine(photos, file), "test path only");
            RoomImages.Init(root);
            var room = new PublicRoom(1, "101", "STD", "Standard", 0, 0, null, null, null, null, 2, 0);
            Assert.Equal("images/rooms/photos/std-1.jpg", RoomImages.Cover(room));
            Assert.Equal(RoomImages.Cover(room), RoomImages.Cover(room with { Id = 9 }));
            Assert.EndsWith("std-2.jpg", RoomImages.Gallery(room)[1]);
            Assert.EndsWith("exe-1.jpg", RoomImages.Cover(room with { RoomTypeCode = "SUITE" }));
            Assert.EndsWith(".svg", RoomImages.Cover(room with { RoomTypeCode = "UNKNOWN" }));
            File.WriteAllText(Path.Combine(photos, "suite-1.jpg"), "test path only");
            RoomImages.Init(root);
            Assert.EndsWith("suite-1.jpg", RoomImages.Cover(room with { RoomTypeCode = "SUITE" }));
        }
        finally
        {
            RoomImages.Init(Path.Combine(root, "absent"));
            Directory.Delete(root, recursive: true);
        }
    }
}
