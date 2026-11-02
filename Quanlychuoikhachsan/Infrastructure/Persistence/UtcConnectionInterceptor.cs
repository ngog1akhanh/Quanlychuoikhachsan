using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Quanlychuoikhachsan.Infrastructure.Persistence;

// Đồng bộ DEFAULT CURRENT_TIMESTAMP với các thời điểm UTC do API ghi.
public sealed class UtcConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SET time_zone = '+00:00';";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SET time_zone = '+00:00';";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
