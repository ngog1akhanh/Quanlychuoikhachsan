using System.Text.Json;
using HotelAdmin.Models;
using Xunit;

namespace Quanlychuoikhachsan.Api.Tests;

public sealed class ReportedBugsTests
{
    [Fact]
    public void Room_lookup_reads_room_board_and_envelopes()
    {
        using var board = JsonDocument.Parse("""{"date":"2030-01-01","rooms":[{"id":12,"room_number":"101"}]}""");
        Assert.Equal(12, J.Long(Assert.Single(J.Items(board.RootElement)), "id"));
        using var envelope = JsonDocument.Parse("""{"data":{"rooms":[{"id":12}]},"request_id":"test"}""");
        Assert.Single(J.Items(J.Unwrap(envelope.RootElement)));
    }
}
