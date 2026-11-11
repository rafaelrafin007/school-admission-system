using System.Data;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldExpirationConcurrencyTests : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new();
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public async Task TwoBatchesDiscoverSameClassLockClassFirstAndRecheckTimeAfterWaiting()
    {
        var admissionClass = await _host.CreateClassAsync(4);
        await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
        var newlyDue = await _host.SeedHoldAsync(admissionClass.Id, "002", "HELD", 3600);
        var future = await _host.SeedHoldAsync(admissionClass.Id, "003", "HELD", 3600);
        var confirmed = await _host.SeedHoldAsync(admissionClass.Id, "004", "CONFIRMED", -600);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await ExpirationTestSupport.LockClass(blocker, admissionClass.Id);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var first = ExpirationTestSupport.ProcessBatch(_host, token: cancellation.Token);
        var second = ExpirationTestSupport.ProcessBatch(_host, token: cancellation.Token);
        try
        {
            // Both batches have completed discovery and are now waiting on the class row.
            await _host.WaitForBlockedRequestsAsync(2);
            // This succeeds only if the processors did not take hold locks before their class lock.
            await blocker.AdmissionHolds.FromSql($"SELECT * FROM admission_holds WHERE class_id = {admissionClass.Id} FOR UPDATE NOWAIT")
                .AsNoTracking().ToListAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = clock_timestamp() WHERE id = {newlyDue}");
            await transaction.CommitAsync();
            var results = await Task.WhenAll(first, second);
            Assert.All(results, result => Assert.Equal(1, result.CandidateClasses));
            Assert.Equal(new long[] { 0, 2 }, results.Select(result => result.ExpiredHolds).Order());
            await using var observer = _host.Database.CreateContext();
            var rows = await observer.AdmissionHolds.AsNoTracking().ToListAsync();
            Assert.Equal(4, rows.Count);
            Assert.Equal(2, rows.Count(h => h.Status == HoldStatus.Expired));
            Assert.Equal(HoldStatus.Held, rows.Single(h => h.Id == future).Status);
            Assert.Equal(HoldStatus.Confirmed, rows.Single(h => h.Id == confirmed).Status);
            Assert.NotNull(rows.Single(h => h.Id == confirmed).ConfirmedAt);
            Assert.All(rows.Where(h => h.Status == HoldStatus.Expired), row =>
            { Assert.Null(row.ConfirmedAt); Assert.Null(row.CancelledAt); });
        }
        finally
        {
            cancellation.Cancel();
            await transaction.DisposeAsync();
            await Drain(first, second);
        }
    }

    [PostgreSqlFact]
    public Task NewReservationRacingExpirationPreservesCapacityAndHistory() => RaceWithApi("reserve");

    [PostgreSqlFact]
    public Task ConfirmationRacingExpirationCannotConfirmOverdueHold() => RaceWithApi("confirm");

    [PostgreSqlFact]
    public Task CancellationRacingExpirationCannotOverwriteExpiredState() => RaceWithApi("cancel");

    private async Task RaceWithApi(string operation)
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await ExpirationTestSupport.LockClass(blocker, admissionClass.Id);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var processor = ExpirationTestSupport.ProcessBatch(_host, token: cancellation.Token);
        var request = _host.SendAsync(operation == "cancel" ? HttpMethod.Delete : HttpMethod.Post,
            operation == "reserve" ? $"/classes/{admissionClass.Id}/holds" : $"/holds/{id}" + (operation == "confirm" ? "/confirm" : ""),
            "001", cancellation.Token);
        try
        {
            await _host.WaitForBlockedRequestsAsync(2);
            await transaction.CommitAsync();
            using var response = await request;
            var batch = await processor;
            Assert.InRange(batch.ExpiredHolds, 0, 1);
            Assert.Equal(operation == "reserve" ? HttpStatusCode.Created : HttpStatusCode.Conflict, response.StatusCode);
            await using var observer = _host.Database.CreateContext();
            var rows = await observer.AdmissionHolds.AsNoTracking().ToListAsync();
            var old = rows.Single(h => h.Id == id);
            Assert.Equal(HoldStatus.Expired, old.Status);
            Assert.Null(old.ConfirmedAt);
            Assert.Null(old.CancelledAt);
            Assert.Equal(operation == "reserve" ? 2 : 1, rows.Count);
            if (operation == "reserve")
            {
                var replacement = Assert.Single(rows, h => h.Status == HoldStatus.Held);
                var now = await observer.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
                Assert.True(replacement.ExpiresAt > now);
            }
            Assert.Equal(operation == "reserve" ? 1 : 0, rows.Count(h => h.Status is HoldStatus.Held or HoldStatus.Confirmed));
        }
        finally
        {
            cancellation.Cancel();
            await transaction.DisposeAsync();
            await Drain(processor, request);
            if (request.IsCompletedSuccessfully) request.Result.Dispose();
        }
    }

    [PostgreSqlFact]
    public async Task AnotherClassCanExpireWhileFirstProcessorWaits()
    {
        var firstClass = await _host.CreateClassAsync(1);
        var secondClass = await _host.CreateClassAsync(1);
        var firstHold = await _host.SeedHoldAsync(firstClass.Id, "001", "HELD", -600);
        var secondHold = await _host.SeedHoldAsync(secondClass.Id, "001", "HELD", -600);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await ExpirationTestSupport.LockClass(blocker, firstClass.Id);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = ExpirationTestSupport.ProcessClass(_host, firstClass.Id, cancellation.Token);
        try
        {
            await _host.WaitForBlockedRequestsAsync(1);
            Assert.Equal(1, await ExpirationTestSupport.ProcessClass(_host, secondClass.Id, cancellation.Token));
            Assert.False(pending.IsCompleted);
            await using var observer = _host.Database.CreateContext();
            Assert.Equal(HoldStatus.Held, (await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == firstHold)).Status);
            Assert.Equal(HoldStatus.Expired, (await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == secondHold)).Status);
            await transaction.CommitAsync();
            Assert.Equal(1, await pending);
            Assert.Equal(2, await observer.AdmissionHolds.CountAsync(h => h.Status == HoldStatus.Expired));
        }
        finally { cancellation.Cancel(); await transaction.DisposeAsync(); await Drain(pending); }
    }

    private static async Task Drain(params Task[] tasks)
    {
        try { await Task.WhenAll(tasks); }
        catch { /* Preserve the original test/request failure during cleanup. */ }
    }
}
