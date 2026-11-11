using System.Data;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Enums;
using Xunit.Abstractions;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldConcurrencyTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new() { DurationSeconds = "3600" };
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public async Task HundredConcurrentStudents_CannotExceedTenSeats()
    {
        await RunContention(capacity: 10, requests: 100, sameStudent: false, expectedSuccesses: 10);
    }

    [PostgreSqlFact]
    public async Task ConcurrentRequestsForSameStudent_CreateExactlyOneHold()
    {
        await RunContention(capacity: 10, requests: 30, sameStudent: true, expectedSuccesses: 1);
    }

    private async Task RunContention(int capacity, int requests, bool sameStudent, int expectedSuccesses)
    {
        var admissionClass = await _host.CreateClassAsync(capacity);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.AdmissionClasses.FromSql($"SELECT * FROM admission_classes WHERE id = {admissionClass.Id} FOR UPDATE")
            .AsNoTracking().ToListAsync();

        // All HTTP tasks wait at this start gate. A real database lock then ensures overlap at the reservation boundary.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Enumerable.Range(0, requests).Select(async index =>
        {
            await start.Task;
            return await _host.ReserveAsync(admissionClass.Id, sameStudent ? "000same-student" : index.ToString("D17"));
        }).ToArray();
        start.SetResult();
        try
        {
            await _host.WaitForBlockedRequestsAsync(minimum: 2);
            await transaction.CommitAsync();
        }
        catch (Exception originalException)
        {
            // Release the test's blocker and drain HTTP requests before fixture cleanup.
            try
            {
                await transaction.RollbackAsync();
                var outstanding = await Task.WhenAll(pending);
                foreach (var response in outstanding) response.Dispose();
            }
            catch (Exception cleanupException) { originalException.Data["CleanupException"] = cleanupException; }
            throw;
        }

        var responses = await Task.WhenAll(pending);
        try
        {
            Assert.Equal(expectedSuccesses, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(requests - expectedSuccesses, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));

            await using var observer = _host.Database.CreateContext();
            var holds = await observer.AdmissionHolds.AsNoTracking().Where(hold => hold.ClassId == admissionClass.Id).ToListAsync();
            var databaseTime = await observer.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
            Assert.Equal(expectedSuccesses, holds.Count);
            Assert.Equal(expectedSuccesses, holds.Select(hold => hold.StudentId).Distinct().Count());
            Assert.All(holds, hold =>
            {
                Assert.Equal(HoldStatus.Held, hold.Status);
                Assert.True(hold.ExpiresAt > databaseTime);
            });
            var occupied = holds.Count(hold => hold.Status == HoldStatus.Confirmed ||
                (hold.Status == HoldStatus.Held && hold.ExpiresAt > databaseTime));
            Assert.Equal(expectedSuccesses, occupied);
            Assert.True(occupied <= capacity);
            output.WriteLine($"Requests={requests}; Created={expectedSuccesses}; Conflict={requests - expectedSuccesses}; ServerErrors=0; Occupied={occupied}; Capacity={capacity}. At least two requests were observed waiting on PostgreSQL locks before release.");
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [PostgreSqlFact]
    public async Task WaitingReservation_UsesFreshPostLockTimeAndSnapshot_WhileOtherClassRemainsAvailable()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var otherClass = await _host.CreateClassAsync(1);
        var originalHold = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        await using var blocker = _host.Database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.AdmissionClasses.FromSql($"SELECT * FROM admission_classes WHERE id = {admissionClass.Id} FOR UPDATE")
            .AsNoTracking().ToListAsync();

        var waiting = _host.ReserveAsync(admissionClass.Id, "001");
        DateTime releaseTime;
        try
        {
            await _host.WaitForBlockedRequestsAsync(minimum: 1);
            Assert.False(waiting.IsCompleted);
            using var unrelated = await _host.ReserveAsync(otherClass.Id, "001").WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Created, unrelated.StatusCode);

            // This expiration change commits after the waiting request's transaction began.
            await blocker.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = clock_timestamp() WHERE id = {originalHold}");
            releaseTime = await blocker.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
            await transaction.CommitAsync();
        }
        catch (Exception originalException)
        {
            try
            {
                await transaction.RollbackAsync();
                using var drained = await waiting;
            }
            catch (Exception cleanupException) { originalException.Data["CleanupException"] = cleanupException; }
            throw;
        }

        using var response = await waiting;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var observer = _host.Database.CreateContext();
        var rows = await observer.AdmissionHolds.AsNoTracking().Where(hold => hold.ClassId == admissionClass.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(HoldStatus.Expired, rows.Single(hold => hold.Id == originalHold).Status);
        var replacement = rows.Single(hold => hold.Id != originalHold);
        Assert.Equal(HoldStatus.Held, replacement.Status);
        Assert.True(replacement.CreatedAt >= releaseTime);
        Assert.Equal(replacement.CreatedAt.AddSeconds(3600), replacement.ExpiresAt);
        output.WriteLine("Observed a PostgreSQL lock wait; the waiting reservation saw the newly committed expiration and used a database timestamp after lock release. Another class reserved successfully while the first remained locked.");
    }
}
