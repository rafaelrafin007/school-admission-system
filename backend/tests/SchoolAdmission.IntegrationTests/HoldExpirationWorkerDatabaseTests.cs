using System.Data;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Expiration;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldExpirationWorkerDatabaseTests
{
    [PostgreSqlFact]
    public Task RegisteredApiWorkerAutomaticallyExpiresAcrossMultipleCycles() => RunAutomaticWorker(failFirstCycle: false);

    [PostgreSqlFact]
    public Task RegisteredWorkerRecoversFromInjectedTransientFailureAndProcessesDatabaseRecords() => RunAutomaticWorker(failFirstCycle: true);

    private static async Task RunAutomaticWorker(bool failFirstCycle)
    {
        var cycles = Channel.CreateUnbounded<HoldExpirationBatchResult>();
        var log = new ExpirationTestLog<HoldExpirationWorker>();
        var failNext = failFirstCycle ? 1 : 0;
        var host = new HoldApiTestHost
        {
            EnableExpirationWorker = true,
            ConfigureServices = services =>
            {
                services.RemoveAll<HoldExpirationSettings>();
                services.AddSingleton(new HoldExpirationSettings(intervalSeconds: 1, batchSize: 1));
                services.AddSingleton<ILogger<HoldExpirationWorker>>(log);
                services.RemoveAll<IHoldExpirationProcessor>();
                services.AddScoped<IHoldExpirationProcessor>(provider => new ObservedProcessor(
                    new HoldExpirationProcessor(provider.GetRequiredService<ApplicationDbContext>(),
                        provider.GetRequiredService<HoldExpirationSettings>(), NullLogger<HoldExpirationProcessor>.Instance),
                    cycles.Writer, () => Interlocked.Exchange(ref failNext, 0) == 1));
            }
        };
        HoldExpirationWorker? worker = null;
        try
        {
            await host.InitializeAsync();
            worker = Assert.Single(host.Factory.Services.GetServices<IHostedService>().OfType<HoldExpirationWorker>());
            if (failFirstCycle) await log.ErrorLogged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var first = await host.CreateClassAsync(1);
            var second = await host.CreateClassAsync(1);
            await host.SeedHoldAsync(first.Id, "001", "HELD", -600);
            await host.SeedHoldAsync(second.Id, "001", "HELD", -600);

            // No student API activity occurs after seeding. Await actual worker cycle notifications.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            for (var processed = 0; processed < 2;)
            {
                var result = await cycles.Reader.ReadAsync(timeout.Token);
                Assert.InRange(result.CandidateClasses, 0, 1);
                if (result.ExpiredHolds == 0) continue;
                Assert.Equal(1, result.ExpiredHolds);
                processed++;
            }
            await using var observer = host.Database.CreateContext();
            var rows = await observer.AdmissionHolds.AsNoTracking().ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Equal(HoldStatus.Expired, row.Status);
                Assert.Null(row.ConfirmedAt);
                Assert.Null(row.CancelledAt);
            });
        }
        finally { await host.DisposeAsync(); }
        Assert.NotNull(worker);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(failFirstCycle ? 1 : 0, log.Entries.Count(entry => entry.Level >= LogLevel.Error));
        Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("private-database-diagnostic"));
    }

    [PostgreSqlFact]
    public async Task ShutdownCancelsDatabaseLockWaitRollsBackAndReleasesConnections()
    {
        var host = new HoldApiTestHost();
        try
        {
            await host.InitializeAsync();
            var admissionClass = await host.CreateClassAsync(1);
            var id = await host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
            await using var blocker = host.Database.CreateContext();
            await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await ExpirationTestSupport.LockClass(blocker, admissionClass.Id);
            var services = new ServiceCollection();
            services.AddScoped(_ => ExpirationTestSupport.CreateContext(host));
            services.AddSingleton(new HoldExpirationSettings());
            services.AddSingleton<ILogger<HoldExpirationProcessor>>(NullLogger<HoldExpirationProcessor>.Instance);
            services.AddScoped<IHoldExpirationProcessor, HoldExpirationProcessor>();
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            var log = new ExpirationTestLog<HoldExpirationWorker>();
            using var worker = new HoldExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new(), log);
            try
            {
                await worker.StartAsync(CancellationToken.None);
                await host.WaitForBlockedRequestsAsync(1);
            }
            finally { await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }
            Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
            await host.WaitForNoTransactionsAsync();
            Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Error);
            await transaction.RollbackAsync();
            await using var observer = host.Database.CreateContext();
            Assert.Equal(HoldStatus.Held, (await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == id)).Status);
            Assert.Equal(1, await ExpirationTestSupport.ProcessClass(host, admissionClass.Id).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(HoldStatus.Expired, (await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == id)).Status);
        }
        finally { await host.DisposeAsync(); }
    }

    private sealed class ObservedProcessor(HoldExpirationProcessor processor, ChannelWriter<HoldExpirationBatchResult> cycles,
        Func<bool> fail) : IHoldExpirationProcessor
    {
        public async Task<HoldExpirationBatchResult> ProcessBatchAsync(CancellationToken cancellationToken)
        {
            if (fail()) throw new NpgsqlException("private-database-diagnostic");
            var result = await processor.ProcessBatchAsync(cancellationToken);
            await cycles.WriteAsync(result, cancellationToken);
            return result;
        }
    }
}
