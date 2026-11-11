using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldApiTests : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new();
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public async Task Create_ReturnsExactContractAndPersistsDatabaseTimeWithDefaultDuration()
    {
        var admissionClass = await _host.CreateClassAsync(2);
        await using var context = _host.Database.CreateContext();
        var before = await context.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        using var response = await _host.ReserveAsync(admissionClass.Id, "  00152691234567890  ");
        var after = await context.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = body.RootElement;
        Assert.Equal(new[] { "classId", "expiresAt", "holdId", "status", "studentId" },
            json.EnumerateObject().Select(property => property.Name).Order());
        var id = json.GetProperty("holdId").GetString()!;
        Assert.StartsWith("h_", id);
        Assert.True(Guid.TryParseExact(id[2..], "N", out _));
        Assert.Equal(admissionClass.Id, json.GetProperty("classId").GetString());
        Assert.Equal("00152691234567890", json.GetProperty("studentId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());

        var stored = await context.AdmissionHolds.AsNoTracking().SingleAsync(hold => hold.Id == id);
        Assert.Equal("00152691234567890", stored.StudentId);
        Assert.Equal(HoldStatus.Held, stored.Status);
        Assert.InRange(stored.CreatedAt, before, after);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
        Assert.Equal(stored.CreatedAt.AddSeconds(600), stored.ExpiresAt);
        Assert.Equal(stored.ExpiresAt, json.GetProperty("expiresAt").GetDateTime());
    }

    [PostgreSqlFact]
    public async Task Create_UsesConfiguredDurationAndAcceptsShortNondigitIdentifiers()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        using var factory = _host.Factory.WithWebHostBuilder(builder => builder.UseSetting("HOLD_DURATION_SECONDS", "37"));
        using var client = factory.CreateClient();
        using var response = await _host.ReserveAsync(admissionClass.Id, "  00student-A  ", client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var context = _host.Database.CreateContext();
        var hold = await context.AdmissionHolds.SingleAsync();
        Assert.Equal("00student-A", hold.StudentId);
        Assert.Equal(TimeSpan.FromSeconds(37), hold.ExpiresAt - hold.CreatedAt);
    }

    [PostgreSqlFact]
    public async Task InvalidHeadersReturn400AndInvalidOrMissingClassesReturn404()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        foreach (var studentId in new string?[] { null, "", "   ", "123456789012345678" })
        {
            using var response = await _host.ReserveAsync(admissionClass.Id, studentId);
            await AssertProblem(response, HttpStatusCode.BadRequest);
        }
        using var multiple = new HttpRequestMessage(HttpMethod.Post, $"/classes/{admissionClass.Id}/holds");
        multiple.Headers.TryAddWithoutValidation("X-Student-Id", new[] { "student-a", "student-b" });
        using var multipleResponse = await _host.Client.SendAsync(multiple);
        await AssertProblem(multipleResponse, HttpStatusCode.BadRequest);

        foreach (var classId in new[] { "invalid", "c_", "c_missing", "c_" + new string('x', 39) })
        {
            using var response = await _host.ReserveAsync(classId, "001");
            await AssertProblem(response, HttpStatusCode.NotFound);
        }
        await using var context = _host.Database.CreateContext();
        Assert.Empty(await context.AdmissionHolds.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task DuplicateHeldAndConfirmedApplicationsConflictAndFullClassRejectsOtherStudents()
    {
        var admissionClass = await _host.CreateClassAsync(2);
        using var first = await _host.ReserveAsync(admissionClass.Id, "student-A");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await _host.ReserveAsync(admissionClass.Id, " student-A ");
        await AssertProblem(duplicate, HttpStatusCode.Conflict);
        await _host.SeedHoldAsync(admissionClass.Id, "student-B", "CONFIRMED", -600);
        using var confirmedDuplicate = await _host.ReserveAsync(admissionClass.Id, "student-B");
        await AssertProblem(confirmedDuplicate, HttpStatusCode.Conflict);
        using var full = await _host.ReserveAsync(admissionClass.Id, "student-C");
        await AssertProblem(full, HttpStatusCode.Conflict);
        await using var context = _host.Database.CreateContext();
        Assert.Equal(2, await context.AdmissionHolds.CountAsync());
    }

    [PostgreSqlFact]
    public async Task StudentsCanShareClassAndApplyToAnotherClass()
    {
        var firstClass = await _host.CreateClassAsync(2);
        var otherClass = await _host.CreateClassAsync(1);
        using var first = await _host.ReserveAsync(firstClass.Id, "001");
        using var second = await _host.ReserveAsync(firstClass.Id, "002");
        using var other = await _host.ReserveAsync(otherClass.Id, "001");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        await using var context = _host.Database.CreateContext();
        var holds = await context.AdmissionHolds.ToListAsync();
        Assert.Equal(3, holds.Count);
        Assert.Equal(3, holds.Select(hold => hold.Id).Distinct().Count());
    }

    [PostgreSqlFact]
    public async Task HistoricalAndOverdueApplicationsDoNotBlockAndExpirationIsClassScoped()
    {
        var admissionClass = await _host.CreateClassAsync(3);
        var otherClass = await _host.CreateClassAsync(1);
        var overdue = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
        var unrelated = await _host.SeedHoldAsync(otherClass.Id, "001", "HELD", -600);
        await _host.SeedHoldAsync(admissionClass.Id, "002", "EXPIRED", -600);
        await _host.SeedHoldAsync(admissionClass.Id, "003", "CANCELLED", 3600);
        foreach (var studentId in new[] { "001", "002", "003" })
        {
            using var response = await _host.ReserveAsync(admissionClass.Id, studentId);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        await using var context = _host.Database.CreateContext();
        Assert.Equal(HoldStatus.Expired, (await context.AdmissionHolds.SingleAsync(hold => hold.Id == overdue)).Status);
        Assert.Equal(HoldStatus.Held, (await context.AdmissionHolds.SingleAsync(hold => hold.Id == unrelated)).Status);
        Assert.Equal(3, await context.AdmissionHolds.CountAsync(hold => hold.ClassId == admissionClass.Id && hold.Status == HoldStatus.Held));
    }

    [PostgreSqlFact]
    public async Task InsertFailureRollsBackExpirationAndReturnsSafe500()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var overdue = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", -600);
        _host.InsertFailure.FailNextHoldInsert = true;
        using var response = await _host.ReserveAsync(admissionClass.Id, "001");
        await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("Injected", await response.Content.ReadAsStringAsync());
        Assert.False(_host.InsertFailure.FailNextHoldInsert);
        await using var context = _host.Database.CreateContext();
        var remaining = Assert.Single(await context.AdmissionHolds.AsNoTracking().ToListAsync());
        Assert.Equal(overdue, remaining.Id);
        Assert.Equal(HoldStatus.Held, remaining.Status);
        using var retry = await _host.ReserveAsync(admissionClass.Id, "001");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("stackTrace", out _));
    }
}
