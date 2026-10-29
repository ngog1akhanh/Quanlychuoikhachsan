using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Quanlychuoikhachsan.Infrastructure.Persistence;

namespace Quanlychuoikhachsan.Application.Common;

/// <summary>Ghép thao tác khách hàng, booking và cọc trong cùng giao dịch.</summary>
public sealed class BookingTransaction : IAsyncDisposable
{
    private readonly IDbContextTransaction? owned;
    private BookingTransaction(IDbContextTransaction? owned) => this.owned = owned;

    public static async Task<BookingTransaction> BeginAsync(HotelDbContext db, CancellationToken ct)
        => new(db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null);

    public Task CommitAsync(CancellationToken ct) => owned?.CommitAsync(ct) ?? Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct) => owned?.RollbackAsync(ct) ?? Task.CompletedTask;
    public ValueTask DisposeAsync() => owned?.DisposeAsync() ?? ValueTask.CompletedTask;
}
