using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;
using SchoolAdmission.Infrastructure.BackgroundJobs;

namespace SchoolAdmission.IntegrationTests.Expiry;

/// <summary>The sweeper loop itself, with a scripted ISender and a fake clock (no database).</summary>
public sealed class HoldExpirySweeperUnitTests
{
    /// <summary>Returns scripted results (or throws) for ExpireStaleHoldsCommand; records every call.</summary>
    private sealed class ScriptedSender(Func<int, int> onCall) : ISender
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Assert.IsType<ExpireStaleHoldsCommand>(request);
            var call = Interlocked.Increment(ref _calls);
            return Task.FromResult((TResponse)(object)onCall(call));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static (HoldExpirySweeper sweeper, FakeTimeProvider clock) Create(ScriptedSender sender, bool enabled = true, int interval = 5)
    {
        var services = new ServiceCollection().AddSingleton<ISender>(sender).BuildServiceProvider();
        var clock = new FakeTimeProvider();
        var options = Options.Create(new HoldOptions { SweeperEnabled = enabled, SweepIntervalSeconds = interval });
        var sweeper = new HoldExpirySweeper(services.GetRequiredService<IServiceScopeFactory>(), options, clock,
            NullLogger<HoldExpirySweeper>.Instance);
        return (sweeper, clock);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "condition not reached within 5 s");
    }

    [Fact]
    public async Task SweepsAtStart_ThenOncePerInterval()
    {
        var sender = new ScriptedSender(_ => 0);
        var (sweeper, clock) = Create(sender, interval: 5);

        await sweeper.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => sender.Calls == 1);

        clock.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(100);
        Assert.Equal(1, sender.Calls);                    // not yet

        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => sender.Calls == 2);    // tick at 5 s

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => sender.Calls == 3);    // tick at 10 s

        await sweeper.StopAsync(CancellationToken.None);  // stops cleanly
    }

    [Fact]
    public async Task FailedSweep_IsLogged_AndTheNextTickStillRuns()
    {
        var sender = new ScriptedSender(call => call == 1 ? throw new InvalidOperationException("database down") : 0);
        var (sweeper, clock) = Create(sender, interval: 5);

        await sweeper.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => sender.Calls == 1);    // first sweep throws

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => sender.Calls == 2);    // loop survived

        Assert.NotNull(sweeper.ExecuteTask);
        Assert.False(sweeper.ExecuteTask!.IsFaulted);
        await sweeper.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Disabled_NeverSweeps()
    {
        var sender = new ScriptedSender(_ => 0);
        var (sweeper, clock) = Create(sender, enabled: false);

        await sweeper.StartAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(200);

        Assert.Equal(0, sender.Calls);
        await sweeper.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OneTick_DrainsFullBatches_UntilAShortOne()
    {
        var sender = new ScriptedSender(call => call < 3 ? HoldExpirySweeper.BatchSize : 7); // 500, 500, 7
        var (sweeper, _) = Create(sender);

        await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(3, sender.Calls);
    }

    [Fact]
    public async Task OneTick_StopsAtTheBatchCap()
    {
        var sender = new ScriptedSender(_ => HoldExpirySweeper.BatchSize); // endless backlog
        var (sweeper, _) = Create(sender);

        await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(HoldExpirySweeper.MaxBatchesPerTick, sender.Calls);
    }
}
