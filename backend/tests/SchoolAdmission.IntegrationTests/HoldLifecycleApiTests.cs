using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldLifecycleApiTests : IAsyncLifetime
{
    private readonly HoldApiTestHost _host = new();
    public Task InitializeAsync() => _host.InitializeAsync();
    public Task DisposeAsync() => _host.DisposeAsync();

    [PostgreSqlFact]
    public async Task GetReturnsExactContractAndEffectiveStatusWithoutChangingRowsOrRequiringIdentity()
    {
        var admissionClass = await _host.CreateClassAsync(10);
        foreach (var (status, seconds, expected) in new[] {
            ("HELD", 3600, "HELD"), ("HELD", -60, "EXPIRED"), ("CONFIRMED", -60, "CONFIRMED"),
            ("CANCELLED", 3600, "CANCELLED"), ("EXPIRED", -60, "EXPIRED") })
        {
            var id = await _host.SeedHoldAsync(admissionClass.Id, Guid.NewGuid().ToString("N")[..17], status, seconds);
            using var response = await _host.Client.GetAsync($"/holds/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            AssertProperties(body.RootElement, "classId", "expiresAt", "holdId", "status");
            Assert.Equal(id, body.RootElement.GetProperty("holdId").GetString());
            Assert.Equal(admissionClass.Id, body.RootElement.GetProperty("classId").GetString());
            Assert.Equal(expected, body.RootElement.GetProperty("status").GetString());
            await using var observer = _host.Database.CreateContext();
            var persisted = await observer.AdmissionHolds.SingleAsync(h => h.Id == id);
            Assert.Equal(status, persisted.Status.ToString().ToUpperInvariant());
            Assert.Equal(persisted.ExpiresAt, body.RootElement.GetProperty("expiresAt").GetDateTime());
        }
        foreach (var id in new[] { "h_missing", "invalid" })
        {
            using var response = await _host.Client.GetAsync($"/holds/{id}");
            await AssertProblem(response, HttpStatusCode.NotFound);
        }
    }

    [PostgreSqlFact]
    public async Task ConfirmIsOwnedIdempotentAndUsesDatabaseTime()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        using var wrongOwner = await _host.SendAsync(HttpMethod.Post, $"/holds/{id}/confirm", "other");
        await AssertProblem(wrongOwner, HttpStatusCode.Forbidden);
        await using var observer = _host.Database.CreateContext();
        Assert.Equal(HoldStatus.Held, (await observer.AdmissionHolds.AsNoTracking().SingleAsync()).Status);
        var before = await DatabaseTime();
        using var first = await _host.SendAsync(HttpMethod.Post, $"/holds/{id}/confirm", " 001 ");
        var after = await DatabaseTime();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var json = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        AssertProperties(json.RootElement, "holdId", "status");
        Assert.Equal(id, json.RootElement.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", json.RootElement.GetProperty("status").GetString());
        var confirmed = await observer.AdmissionHolds.AsNoTracking().SingleAsync();
        Assert.Equal(HoldStatus.Confirmed, confirmed.Status);
        Assert.InRange(confirmed.ConfirmedAt!.Value, before, after);
        Assert.Equal(DateTimeKind.Utc, confirmed.ConfirmedAt.Value.Kind);
        Assert.Null(confirmed.CancelledAt);
        using var repeat = await _host.SendAsync(HttpMethod.Post, $"/holds/{id}/confirm", "001");
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        using var wrongRepeat = await _host.SendAsync(HttpMethod.Post, $"/holds/{id}/confirm", "other");
        await AssertProblem(wrongRepeat, HttpStatusCode.Forbidden);
        Assert.Equal(confirmed.ConfirmedAt, (await observer.AdmissionHolds.AsNoTracking().SingleAsync()).ConfirmedAt);
        using var seats = JsonDocument.Parse(await _host.Client.GetStringAsync($"/classes/{admissionClass.Id}"));
        Assert.Equal(1, seats.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, seats.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, seats.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [PostgreSqlFact]
    public async Task CancelPreservesHistoryReleasesSeatAndRepeatedCancellationConflicts()
    {
        var admissionClass = await _host.CreateClassAsync(1);
        var id = await _host.SeedHoldAsync(admissionClass.Id, "001", "HELD", 3600);
        using var forbidden = await _host.SendAsync(HttpMethod.Delete, $"/holds/{id}", "other");
        await AssertProblem(forbidden, HttpStatusCode.Forbidden);
        var before = await DatabaseTime();
        using var response = await _host.SendAsync(HttpMethod.Delete, $"/holds/{id}", " 001 ");
        var after = await DatabaseTime();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        await using var observer = _host.Database.CreateContext();
        var row = await observer.AdmissionHolds.AsNoTracking().SingleAsync();
        Assert.Equal(HoldStatus.Cancelled, row.Status);
        Assert.InRange(row.CancelledAt!.Value, before, after);
        Assert.Null(row.ConfirmedAt);
        using var repeated = await _host.SendAsync(HttpMethod.Delete, $"/holds/{id}", "001");
        await AssertProblem(repeated, HttpStatusCode.Conflict);
        Assert.Equal(row.CancelledAt, (await observer.AdmissionHolds.AsNoTracking().SingleAsync()).CancelledAt);
        using var seats = JsonDocument.Parse(await _host.Client.GetStringAsync($"/classes/{admissionClass.Id}"));
        Assert.Equal(1, seats.RootElement.GetProperty("availableSeats").GetInt32());
        using var replacement = await _host.ReserveAsync(admissionClass.Id, "001");
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        Assert.Equal(2, await observer.AdmissionHolds.CountAsync());
    }

    [PostgreSqlFact]
    public async Task InvalidStatesConflictAndOverdueHeldRowsArePersistedAsExpired()
    {
        var admissionClass = await _host.CreateClassAsync(10);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
        foreach (var status in new[] { "HELD", "EXPIRED", "CANCELLED", "CONFIRMED" })
        {
            if (method == HttpMethod.Post && status == "CONFIRMED") continue;
            var id = await _host.SeedHoldAsync(admissionClass.Id, Guid.NewGuid().ToString("N")[..17], status, -60);
            await using var observer = _host.Database.CreateContext();
            var original = await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == id);
            using var response = await _host.SendAsync(method, $"/holds/{id}" + (method == HttpMethod.Post ? "/confirm" : ""), original.StudentId);
            await AssertProblem(response, HttpStatusCode.Conflict);
            var persisted = await observer.AdmissionHolds.AsNoTracking().SingleAsync(h => h.Id == id);
            Assert.Equal(status == "HELD" ? HoldStatus.Expired : original.Status, persisted.Status);
            Assert.Equal(original.ConfirmedAt, persisted.ConfirmedAt);
            Assert.Equal(original.CancelledAt, persisted.CancelledAt);
        }
    }

    [PostgreSqlFact]
    public async Task RequiredIdentityValidationAndMissingHoldsUseConsistentProblems()
    {
        foreach (var (method, path) in new[] { (HttpMethod.Post, "/holds/h_missing/confirm"),
            (HttpMethod.Delete, "/holds/h_missing"), (HttpMethod.Get, "/me/holds") })
        {
            foreach (var student in new string?[] { null, "", "  ", "123456789012345678" })
            {
                using var response = await _host.SendAsync(method, path, student);
                await AssertProblem(response, HttpStatusCode.BadRequest);
            }
            using var request = new HttpRequestMessage(method, path);
            request.Headers.TryAddWithoutValidation("X-Student-Id", new[] { "one", "two" });
            using var multiple = await _host.Client.SendAsync(request);
            await AssertProblem(multiple, HttpStatusCode.BadRequest);
            if (method != HttpMethod.Get)
            {
                using var missing = await _host.SendAsync(method, path, "001");
                await AssertProblem(missing, HttpStatusCode.NotFound);
                using var invalid = await _host.SendAsync(method, path.Replace("h_missing", "invalid"), "001");
                await AssertProblem(invalid, HttpStatusCode.NotFound);
            }
        }
    }

    [PostgreSqlFact]
    public async Task HistoryIsFilteredOrderedAndContainsAllEffectiveStatusesAndClassNames()
    {
        using var empty = await _host.SendAsync(HttpMethod.Get, "/me/holds", "001");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("[]", await empty.Content.ReadAsStringAsync());
        var ids = new List<string>();
        foreach (var status in new[] { "HELD", "CONFIRMED", "CANCELLED", "EXPIRED", "HELD" })
        {
            var admissionClass = await _host.CreateClassAsync(2);
            ids.Add(await _host.SeedHoldAsync(admissionClass.Id, "001", status, ids.Count == 4 ? -60 : 3600));
            await _host.SeedHoldAsync(admissionClass.Id, "1", "HELD", 3600);
        }
        using var response = await _host.SendAsync(HttpMethod.Get, "/me/holds", " 001 ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.EnumerateArray().ToArray();
        Assert.Equal(ids.AsEnumerable().Reverse(), rows.Select(r => r.GetProperty("holdId").GetString()));
        Assert.Equal(new[] { "EXPIRED", "EXPIRED", "CANCELLED", "CONFIRMED", "HELD" }, rows.Select(r => r.GetProperty("status").GetString()));
        foreach (var row in rows)
        {
            AssertProperties(row, "classId", "className", "expiresAt", "holdId", "status");
            Assert.Equal("Class 6 - Morning", row.GetProperty("className").GetString());
        }
        await using var observer = _host.Database.CreateContext();
        Assert.Equal(HoldStatus.Held, (await observer.AdmissionHolds.SingleAsync(h => h.Id == ids[4])).Status);
    }

    private async Task<DateTime> DatabaseTime()
    {
        await using var context = _host.Database.CreateContext();
        return await context.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
    }

    private static void AssertProperties(JsonElement value, params string[] names) =>
        Assert.Equal(names.Order(), value.EnumerateObject().Select(p => p.Name).Order());

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("stackTrace", out _));
    }
}
