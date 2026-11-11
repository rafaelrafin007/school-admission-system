using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Infrastructure.Expiration;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldExpirationWorkerTests
{
    [Fact]
    public async Task StartDoesNotBlockAndStopCancelsProcessingAndDisposesScopeWithoutError()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = 0;
        var services = new ServiceCollection().AddScoped<IHoldExpirationProcessor>(_ => new TestProcessor(async token =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HoldExpirationBatchResult(0, 0);
        }, () => Interlocked.Increment(ref disposed)));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var log = new ExpirationTestLog<HoldExpirationWorker>();
        using var worker = new HoldExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new(), log);
        try
        {
            await worker.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(1, disposed);
        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task FailureRetriesAfterIntervalWithFreshScopeAndStopsDuringLongDelay()
    {
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var scopes = 0;
        var disposed = 0;
        long firstTime = 0;
        long secondTime = 0;
        var services = new ServiceCollection().AddScoped<IHoldExpirationProcessor>(_ =>
        {
            Interlocked.Increment(ref scopes);
            return new TestProcessor(token =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstTime = Stopwatch.GetTimestamp();
                    throw new InvalidOperationException("private-connection-diagnostic");
                }
                secondTime = Stopwatch.GetTimestamp();
                second.TrySetResult();
                return Task.FromResult(new HoldExpirationBatchResult(1, 2));
            }, () => Interlocked.Increment(ref disposed));
        });
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var log = new ExpirationTestLog<HoldExpirationWorker>();
        using var worker = new HoldExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new(intervalSeconds: 1), log);
        try
        {
            await worker.StartAsync(CancellationToken.None);
            await second.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(Stopwatch.GetElapsedTime(firstTime, secondTime) >= TimeSpan.FromSeconds(1));
        Assert.Equal(2, calls);
        Assert.Equal(2, scopes);
        Assert.Equal(2, disposed);
        Assert.Single(log.Entries, entry => entry.Level == LogLevel.Error);
        Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("private-connection-diagnostic"));
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task MaximumConfiguredIntervalRemainsCancellable()
    {
        var completedCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection().AddScoped<IHoldExpirationProcessor>(_ => new TestProcessor(token =>
            Task.FromResult(new HoldExpirationBatchResult(0, 0)), () => completedCycle.TrySetResult()));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var log = new ExpirationTestLog<HoldExpirationWorker>();
        using var worker = new HoldExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new(intervalSeconds: int.MaxValue), log);
        try
        {
            await worker.StartAsync(CancellationToken.None);
            await completedCycle.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Error);
    }

    private sealed class TestProcessor(Func<CancellationToken, Task<HoldExpirationBatchResult>> process, Action dispose)
        : IHoldExpirationProcessor, IDisposable
    {
        public Task<HoldExpirationBatchResult> ProcessBatchAsync(CancellationToken cancellationToken) => process(cancellationToken);
        public void Dispose() => dispose();
    }
}
