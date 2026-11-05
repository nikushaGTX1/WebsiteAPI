using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;

namespace Website_API.Services;

// A session advisory lock serializes paid requests across HTTP calls, workers and Railway replicas.
// It also serializes budget reservations and duplicate checks. Connection disposal releases it on crash.
public interface IImageEditingGuard
{
    Task<IAsyncDisposable?> TryEnterAsync(CancellationToken ct);
}
public sealed class ImageEditingGuard(AppDbContext context) : IImageEditingGuard
{
    public async Task<IAsyncDisposable?> TryEnterAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        try
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(865214900172)", connection);
            if (await command.ExecuteScalarAsync(ct) is true) return new Lease(connection);
            await connection.DisposeAsync();
            return null;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class Lease(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(865214900172)", connection);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
