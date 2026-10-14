using Microsoft.EntityFrameworkCore;
using Npgsql;
using Website_API.Data;
namespace Website_API.Services;
public class MyHomeWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<MyHomeWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Process(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) { log.LogError(error, "MyHome worker failed; persisted pending checks will resume"); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch (OperationCanceledException) { break; }
        }
    }
    private async Task Process(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync(token);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(78452120)", connection);
        if (!(bool)(await command.ExecuteScalarAsync(token))!) return;
        try
        {
            var job = await db.MyHomeJobs.FirstOrDefaultAsync(j => j.State == "RUNNING" && j.Mode == "SERVER", token);
            if (job == null) return;
            job.Error = null;
            await db.SaveChangesAsync(token);
            var concurrency = Math.Clamp(config.GetValue<int?>("MyHome:Concurrency") ?? 2, 1, 8);
            while (!token.IsCancellationRequested)
            {
                var ids = await db.MyHomeChecks.AsNoTracking().Where(c => c.JobId == job.Id && c.Status == "PENDING").Select(c => c.MyHomeId).Distinct().Take(concurrency).ToListAsync(token);
                if (ids.Count == 0) break;
                var rateLimited = 0;
                await Task.WhenAll(ids.Select(async id =>
                {
                    using var itemScope = scopes.CreateScope();
                    var itemDb = itemScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // Reuse already persisted evidence for this ID within the same job after a restart.
                    var prior = await itemDb.MyHomeChecks.AsNoTracking().FirstOrDefaultAsync(c => c.JobId == job.Id && c.MyHomeId == id && c.Status != "PENDING", token);
                    var result = prior == null ? await itemScope.ServiceProvider.GetRequiredService<MyHomeVerifier>().Verify(id, token) : new MyHomeResult(prior.Status, prior.Detail ?? "", prior.HttpStatus);
                    var checkedAt = prior?.CheckedAt ?? DateTime.UtcNow;
                    if (result.HttpStatus == 429) Interlocked.Exchange(ref rateLimited, 1);
                    await using var transaction = await itemDb.Database.BeginTransactionAsync(token);
                    var apartmentIds = await itemDb.MyHomeChecks.Where(c => c.JobId == job.Id && c.MyHomeId == id).Select(c => c.ApartmentId).ToListAsync(token);
                    await itemDb.MyHomeChecks.Where(c => c.JobId == job.Id && c.MyHomeId == id && c.Status == "PENDING").ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, result.Status).SetProperty(c => c.CheckedAt, checkedAt).SetProperty(c => c.Detail, result.Detail).SetProperty(c => c.HttpStatus, result.HttpStatus), token);
                    // An edit during a job must not attach old evidence to a newly linked listing.
                    await itemDb.Apartments.Where(a => apartmentIds.Contains(a.Id) && a.MyHomeId == id).ExecuteUpdateAsync(set => set.SetProperty(a => a.MyHomeStatus, result.Status).SetProperty(a => a.MyHomeCheckedAt, checkedAt), token);
                    await transaction.CommitAsync(token);
                    log.LogInformation("MyHome job {JobId}, ID {MyHomeId}: {Status}, HTTP {HttpStatus}", job.Id, id, result.Status, result.HttpStatus);
                }));
                if (rateLimited != 0)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(config.GetValue<int?>("MyHome:RateLimitBackoffSeconds") ?? 60, 10, 900)), token);
                else
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(config.GetValue<int?>("MyHome:BatchDelayMs") ?? 1000, 250, 60000)), token);
            }
            token.ThrowIfCancellationRequested();
            job.State = "COMPLETED"; job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(token);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogError(error, "MyHome job interrupted");
            await db.MyHomeJobs.Where(j => j.State == "RUNNING" && j.Mode == "SERVER").ExecuteUpdateAsync(s => s.SetProperty(j => j.Error, "Processing interrupted; retrying automatically."), token);
        }
        finally
        {
            await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(78452120)", connection);
            await unlock.ExecuteScalarAsync(CancellationToken.None);
        }
    }
}
