using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SchoolAdmission.Infrastructure.Expiration;

public sealed class HoldExpirationWorker(IServiceScopeFactory scopeFactory, HoldExpirationSettings settings,
    ILogger<HoldExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IHoldExpirationProcessor>();
                    var result = await processor.ProcessBatchAsync(stoppingToken);
                    logger.LogDebug("Expiration cycle: {CandidateClasses} classes, {ExpiredHolds} holds, {ElapsedMilliseconds} ms.",
                        result.CandidateClasses, result.ExpiredHolds, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogError("Expiration cycle failed ({ErrorType}); retrying after {IntervalSeconds} seconds.",
                        exception.GetType().Name, settings.IntervalSeconds);
                }

                // Chunk large configured intervals to respect the runtime timer range without an arbitrary settings limit.
                var remainingSeconds = settings.IntervalSeconds;
                while (remainingSeconds > 0)
                {
                    var delaySeconds = Math.Min(remainingSeconds, 86400);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                    remainingSeconds -= delaySeconds;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
