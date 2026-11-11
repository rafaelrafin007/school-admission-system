using System.Data;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldLifecycleConcurrencyTests : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new() { DurationSeconds = "3600" };
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public Task ConcurrentConfirmationsAreIdempotent() => RaceTransitions(confirmFirst: true, confirmSecond: true);

    [PostgreSqlFact]
    public Task ConfirmationAndCancellationProduceOneFinalState() => RaceTransitions(confirmFirst: true, confirmSecond: false);

    [PostgreSqlFact]
    public Task ConcurrentCancellationsReleaseOnlyOneSeat() => RaceTransitions(confirmFirst: false, confirmSecond: false);

    private async Task RaceTransitions(bool confirmFirst, bool confirmSecond)
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        var responses = await Race(admissionClass.Id, () => Transition(id, confirmFirst), () => Transition(id, confirmSecond));
        try
        {
            if (confirmFirst && confirmSecond)
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            else
            {
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
                Assert.Single(responses, response => response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
            }
            var row = await Read(id);
            Assert.Equal(responses.Any(response => response.StatusCode == HttpStatusCode.OK) ? HoldStatus.Confirmed : HoldStatus.Cancelled, row.Status);
            Assert.Equal(row.Status == HoldStatus.Confirmed, row.ConfirmedAt.HasValue);
            Assert.Equal(row.Status == HoldStatus.Cancelled, row.CancelledAt.HasValue);
            using var repeat = await Transition(id, confirm: true);
            Assert.Equal(row.Status == HoldStatus.Confirmed ? HttpStatusCode.OK : HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Equal(row.ConfirmedAt, (await Read(id)).ConfirmedAt);
            Assert.Equal(row.CancelledAt, (await Read(id)).CancelledAt);
            using var replacement = await _host.ReserveAsync(admissionClass.Id, "other");
            Assert.Equal(row.Status == HoldStatus.Confirmed ? HttpStatusCode.Conflict : HttpStatusCode.Created, replacement.StatusCode);
            await AssertOccupancy(admissionClass.Id, 1);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgreSqlFact]
    public async Task ConfirmationAndNewReservationSeeExpirationCommittedAfterTheyStarted()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        var responses = await Race(admissionClass.Id, () => Transition(id, confirm: true),
            () => _host.ReserveAsync(admissionClass.Id, "001"), async blocker =>
            {
                await blocker.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = clock_timestamp() WHERE id = {id}");
            });
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, responses[0].StatusCode);
            Assert.Equal(HttpStatusCode.Created, responses[1].StatusCode);
            var old = await Read(id);
            Assert.Equal(HoldStatus.Expired, old.Status);
            Assert.Null(old.ConfirmedAt);
            Assert.Null(old.CancelledAt);
            await using var observer = _host.Database.CreateContext();
            var rows = await observer.AdmissionHolds.AsNoTracking().ToListAsync();
            Assert.Equal(2, rows.Count);
            var replacement = Assert.Single(rows, row => row.Status == HoldStatus.Held);
            Assert.True(replacement.CreatedAt >= old.ExpiresAt);
            Assert.Equal(replacement.CreatedAt.AddSeconds(3600), replacement.ExpiresAt);
            await AssertOccupancy(admissionClass.Id, 1);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgreSqlFact]
    public async Task WaitingConfirmationUsesTimeAfterHoldLockAndRejectsReachedDeadline()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        // The test deliberately holds only the hold row to prove the API reads time after its second lock.
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.AdmissionHolds.FromSql($"SELECT * FROM admission_holds WHERE id = {id} FOR UPDATE").ToListAsync();
        var pending = Transition(id, confirm: true);
        try
        {
            await _host.WaitForBlockedRequestsAsync(1);
            await blocker.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = clock_timestamp() WHERE id = {id}");
            await transaction.CommitAsync();
            using var response = await pending;
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var row = await Read(id);
            Assert.Equal(HoldStatus.Expired, row.Status);
            Assert.Null(row.ConfirmedAt);
            Assert.Null(row.CancelledAt);
        }
        finally
        {
            await transaction.DisposeAsync();
            await Drain(pending);
        }
    }

    [PostgreSqlFact]
    public async Task DifferentClassCancellationProceedsWhileConfirmationWaits()
    {
        var firstClass = await _host.CreateClassAsync(1);
        var secondClass = await _host.CreateClassAsync(1);
        var first = await _host.SeedHoldAsync(firstClass.Id, "001", "HELD", 3600);
        var second = await _host.SeedHoldAsync(secondClass.Id, "001", "HELD", 3600);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockClass(blocker, firstClass.Id);
        var pending = Transition(first, confirm: true);
        try
        {
            await _host.WaitForBlockedRequestsAsync(1);
            using var independent = await Transition(second, confirm: false).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.NoContent, independent.StatusCode);
            Assert.False(pending.IsCompleted);
            await transaction.CommitAsync();
            using var response = await pending;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HoldStatus.Confirmed, (await Read(first)).Status);
            Assert.NotNull((await Read(first)).ConfirmedAt);
            Assert.Equal(HoldStatus.Cancelled, (await Read(second)).Status);
            Assert.NotNull((await Read(second)).CancelledAt);
        }
        finally { await transaction.DisposeAsync(); await Drain(pending); }
    }

    [PostgreSqlFact]
    public async Task RequestCancellationWhileWaitingForClassLockLeavesNoTransition()
    {
        foreach (var confirm in new[] { true, false })
        {
            var admissionClass = await _host.CreateClassAsync(1);
            var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
            await using var blocker = _host.Database.CreateContext();
            await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await LockClass(blocker, admissionClass.Id);
            using var cancellation = new CancellationTokenSource();
            var pending = Transition(id, confirm, cancellation.Token);
            try
            {
                await _host.WaitForBlockedRequestsAsync(1);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
                await _host.WaitForNoTransactionsAsync();
                await transaction.RollbackAsync();
                await AssertUnchangedAndRetry(id, admissionClass.Id, confirm);
            }
            finally { cancellation.Cancel(); await transaction.DisposeAsync(); await Drain(pending); }
        }
    }

    [PostgreSqlFact]
    public async Task RequestCancellationAfterUpdateBeforeCommitRollsBackStatusAndTimestamps()
    {
        foreach (var confirm in new[] { true, false })
        {
            var admissionClass = await _host.CreateClassAsync(1);
            var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
            _host.CommitCancellation.Arm();
            using var cancellation = new CancellationTokenSource();
            var pending = Transition(id, confirm, cancellation.Token);
            try
            {
                await _host.CommitCancellation.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(HoldStatus.Held, (await Read(id)).Status);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
                await _host.WaitForNoTransactionsAsync();
                await AssertUnchangedAndRetry(id, admissionClass.Id, confirm);
            }
            finally { cancellation.Cancel(); await Drain(pending); }
        }
    }

    [PostgreSqlFact]
    public async Task CancelledCreationBeforeCommitRollsBackInsertAndLazyExpiration()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -60);
        _host.CommitCancellation.Arm();
        using var cancellation = new CancellationTokenSource();
        var pending = _host.SendAsync(HttpMethod.Post, $"/classes/{admissionClass.Id}/holds", "001", cancellation.Token);
        try
        {
            await _host.CommitCancellation.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
            await _host.WaitForNoTransactionsAsync();
            await using var observer = _host.Database.CreateContext();
            Assert.Single(await observer.AdmissionHolds.ToListAsync());
            Assert.Equal(HoldStatus.Held, (await Read(id)).Status);
            using var retry = await _host.ReserveAsync(admissionClass.Id, "001").WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal(HoldStatus.Expired, (await Read(id)).Status);
            await AssertOccupancy(admissionClass.Id, 1);
        }
        finally { cancellation.Cancel(); await Drain(pending); }
    }

    private async Task AssertUnchangedAndRetry(string id, string classId, bool confirm)
    {
        var unchanged = await Read(id);
        Assert.Equal(HoldStatus.Held, unchanged.Status);
        Assert.Null(unchanged.ConfirmedAt);
        Assert.Null(unchanged.CancelledAt);
        using var retry = await Transition(id, confirm).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(confirm ? HttpStatusCode.OK : HttpStatusCode.NoContent, retry.StatusCode);
        using var newHold = await _host.ReserveAsync(classId, "other");
        Assert.Equal(confirm ? HttpStatusCode.Conflict : HttpStatusCode.Created, newHold.StatusCode);
        await AssertOccupancy(classId, 1);
    }

    private Task<HttpResponseMessage> Transition(string id, bool confirm, CancellationToken token = default) =>
        _host.SendAsync(confirm ? HttpMethod.Post : HttpMethod.Delete, $"/holds/{id}" + (confirm ? "/confirm" : ""), "001", token);

    private async Task<AdmissionHold> Read(string id)
    {
        await using var observer = _host.Database.CreateContext();
        return await observer.AdmissionHolds.AsNoTracking().SingleAsync(hold => hold.Id == id);
    }

    private async Task AssertOccupancy(string classId, int expected)
    {
        await using var observer = _host.Database.CreateContext();
        var occupied = await observer.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM admission_holds
            WHERE class_id = {classId} AND (status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > statement_timestamp()))
            """).SingleAsync();
        Assert.Equal(expected, occupied);
    }

    private static async Task LockClass(ApplicationDbContext context, string classId) =>
        await context.AdmissionClasses.FromSql($"SELECT * FROM admission_classes WHERE id = {classId} FOR UPDATE")
            .AsNoTracking().ToListAsync();

    private async Task<HttpResponseMessage[]> Race(string classId, Func<Task<HttpResponseMessage>> first,
        Func<Task<HttpResponseMessage>> second, Func<ApplicationDbContext, Task>? beforeRelease = null)
    {
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockClass(blocker, classId);
        var pending = new[] { first(), second() };
        try
        {
            await _host.WaitForBlockedRequestsAsync(2);
            if (beforeRelease is not null) await beforeRelease(blocker);
            await transaction.CommitAsync();
            return await Task.WhenAll(pending);
        }
        catch (Exception original)
        {
            try { await transaction.DisposeAsync(); await Drain(pending); }
            catch (Exception cleanup) { original.Data["CleanupException"] = cleanup; }
            throw;
        }
    }

    private static async Task Drain(params Task<HttpResponseMessage>[] pending)
    {
        try { await Task.WhenAll(pending); }
        catch { /* The assertion/request failure is reported by the caller. */ }
        finally { foreach (var task in pending) if (task.IsCompletedSuccessfully) task.Result.Dispose(); }
    }
}
