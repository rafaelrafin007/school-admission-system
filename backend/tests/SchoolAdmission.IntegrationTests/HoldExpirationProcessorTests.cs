using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldExpirationProcessorTests : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new();
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public async Task ClassProcessingOnlyExpiresEligibleRowsAndPreservesHistory()
    {
        var admissionClass = await _host.CreateClassAsync(10);
        var otherClass = await _host.CreateClassAsync(1);
        var emptyClass = await _host.CreateClassAsync(1);
        var first = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
        var second = await _host.SeedHoldAsync(admissionClass.Id, "002", "HELD", -600);
        await _host.SeedHoldAsync(admissionClass.Id, "003", "HELD", 3600);
        await _host.SeedHoldAsync(admissionClass.Id, "004", "CONFIRMED", -600);
        await _host.SeedHoldAsync(admissionClass.Id, "005", "CANCELLED", -600);
        await _host.SeedHoldAsync(admissionClass.Id, "006", "EXPIRED", -600);
        var other = await _host.SeedHoldAsync(otherClass.Id, "001", "HELD", -600);
        await using var observer = _host.Database.CreateContext();
        var before = await observer.AdmissionHolds.AsNoTracking().ToDictionaryAsync(h => h.Id);
        Assert.Equal(2, await ExpirationTestSupport.ProcessClass(_host, admissionClass.Id));
        var after = await observer.AdmissionHolds.AsNoTracking().ToListAsync();
        Assert.Equal(before.Count, after.Count);
        foreach (var row in after)
        {
            var original = before[row.Id];
            Assert.Equal(row.Id == first || row.Id == second ? HoldStatus.Expired : original.Status, row.Status);
            Assert.Equal(original.CreatedAt, row.CreatedAt);
            Assert.Equal(original.ExpiresAt, row.ExpiresAt);
            Assert.Equal(original.ConfirmedAt, row.ConfirmedAt);
            Assert.Equal(original.CancelledAt, row.CancelledAt);
            Assert.Equal(original.StudentId, row.StudentId);
        }
        Assert.Equal(HoldStatus.Held, after.Single(h => h.Id == other).Status);
        Assert.Equal(0, await ExpirationTestSupport.ProcessClass(_host, admissionClass.Id));
        Assert.Equal(0, await ExpirationTestSupport.ProcessClass(_host, emptyClass.Id));
        Assert.Equal(0, await ExpirationTestSupport.ProcessClass(_host, "c_missing"));
        using var reapplied = await _host.ReserveAsync(admissionClass.Id, "001");
        Assert.Equal(HttpStatusCode.Created, reapplied.StatusCode);
        Assert.Equal(2, await observer.AdmissionHolds.CountAsync(h => h.ClassId == admissionClass.Id && h.StudentId == "001"));
    }

    [PostgreSqlFact]
    public async Task BatchesAreBoundedOldestFirstAndLaterCyclesFinishRemainingClasses()
    {
        var expiredIds = new List<string>();
        foreach (var seconds in new[] { -300, -200, -100 })
        {
            var admissionClass = await _host.CreateClassAsync(1);
            expiredIds.Add(await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", seconds));
        }
        var futureClass = await _host.CreateClassAsync(1);
        var future = await _host.SeedHoldAsync(futureClass.Id, "001", "HELD", 3600);
        var first = await ExpirationTestSupport.ProcessBatch(_host, batchSize: 2);
        Assert.Equal(2, first.CandidateClasses);
        Assert.Equal(2, first.ExpiredHolds);
        await using var observer = _host.Database.CreateContext();
        var rows = await observer.AdmissionHolds.AsNoTracking().ToDictionaryAsync(h => h.Id);
        Assert.Equal(HoldStatus.Expired, rows[expiredIds[0]].Status);
        Assert.Equal(HoldStatus.Expired, rows[expiredIds[1]].Status);
        Assert.Equal(HoldStatus.Held, rows[expiredIds[2]].Status);
        Assert.Equal(HoldStatus.Held, rows[future].Status);
        var second = await ExpirationTestSupport.ProcessBatch(_host, batchSize: 2);
        Assert.Equal(1, second.CandidateClasses);
        Assert.Equal(1, second.ExpiredHolds);
        var third = await ExpirationTestSupport.ProcessBatch(_host, batchSize: 2);
        Assert.Equal(0, third.CandidateClasses);
        Assert.Equal(0, third.ExpiredHolds);
        Assert.Equal(3, await observer.AdmissionHolds.CountAsync(h => h.Status == HoldStatus.Expired));
    }

    [PostgreSqlFact]
    public async Task ExactDatabaseTimeBoundaryIsEligibleButOneMicrosecondLaterIsNot()
    {
        var admissionClass = await _host.CreateClassAsync(2);
        var equal = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -60);
        var later = await _host.SeedHoldAsync(admissionClass.Id, "002", "HELD", 3600);
        await using var observer = _host.Database.CreateContext();
        var boundary = await observer.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        await observer.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = {boundary} WHERE id = {equal}");
        await observer.Database.ExecuteSqlInterpolatedAsync($"UPDATE admission_holds SET expires_at = {boundary.AddTicks(10)} WHERE id = {later}");
        var clock = new FixedDatabaseTime(boundary);
        await using var context = ExpirationTestSupport.CreateContext(_host, clock);
        Assert.Equal(1, await ExpirationTestSupport.Processor(context).ProcessClassAsync(admissionClass.Id, CancellationToken.None));
        Assert.True(clock.Used);
        var rows = await observer.AdmissionHolds.AsNoTracking().ToDictionaryAsync(h => h.Id);
        Assert.Equal(HoldStatus.Expired, rows[equal].Status);
        Assert.Equal(HoldStatus.Held, rows[later].Status);
    }

    // Return a captured PostgreSQL instant from the time query so equality can be tested without sleeps.
    private sealed class FixedDatabaseTime(DateTime boundary) : DbCommandInterceptor
    {
        public bool Used { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("clock_timestamp()", StringComparison.Ordinal))
            {
                Used = true;
                command.CommandText = "SELECT @boundary AS \"Value\"";
                command.Parameters.Clear();
                command.Parameters.Add(new NpgsqlParameter("boundary", boundary));
            }
            return ValueTask.FromResult(result);
        }
    }
}
