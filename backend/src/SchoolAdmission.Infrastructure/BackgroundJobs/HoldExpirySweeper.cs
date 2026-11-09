using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;

namespace SchoolAdmission.Infrastructure.BackgroundJobs;

/// <summary>
/// Every HOLD_SWEEP_INTERVAL_SECONDS, marks HELD holds past expiresAt as EXPIRED in the database.
/// Seats are already free at expiresAt without this (counts are derived at query time); the sweeper only
/// updates stored statuses. Safe to run in several backend instances at once (SKIP LOCKED). A failed sweep
/// (e.g. database down) is logged and retried next tick; it never stops the host.
/// </summary>
public sealed class HoldExpirySweeper(
    IServiceScopeFactory scopes,
    IOptions<HoldOptions> options,
    TimeProvider clock,
    ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    public const int BatchSize = 500;

    /// <summary>Caps one tick's work so a large backlog can't monopolize a connection (rest goes next tick).</summary>
    public const int MaxBatchesPerTick = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SweeperEnabled)
        {
            logger.LogInformation("Hold expiry sweeper is disabled (HOLD_SWEEPER_ENABLED=false)");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
        logger.LogInformation("Hold expiry sweeper started; interval {Interval}s", interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval, clock);
        do
        {
            await SweepOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    /// <summary>One tick: expire batches until a batch comes back short (or the per-tick cap is hit).</summary>
    internal async Task SweepOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var total = 0;
            for (var batch = 0; batch < MaxBatchesPerTick; batch++)
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var expired = await sender.Send(new ExpireStaleHoldsCommand(BatchSize), stoppingToken);
                total += expired;
                if (expired < BatchSize) break;
            }

            if (total > 0) logger.LogInformation("Expired {Count} stale hold(s)", total);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Hold expiry sweep failed; will retry next tick");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false; // host is stopping
        }
    }
}
