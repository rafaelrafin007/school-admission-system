using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

// xUnit creates a new test instance (and therefore a new schema) for each test.
public sealed class ClassApiTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new();
    private readonly CommandRecorder _commands = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlTestDatabase.ConnectionVariable)))
            return;

        try
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
                builder.ConfigureServices(services => services.AddDbContext<ApplicationDbContext>(
                    options => options.AddInterceptors(_commands)));
            });
            _client = _factory.CreateClient();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
            if (_factory is not null)
                await _factory.DisposeAsync();
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Create_ReturnsExactContractLocationUniqueIdsAndPersistsInPostgreSql()
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertProperties(body.RootElement, "id", "name", "totalSeats");
        var id = body.RootElement.GetProperty("id").GetString()!;
        Assert.StartsWith("c_", id);
        Assert.True(Guid.TryParseExact(id[2..], "N", out _));
        Assert.Equal("Class 6 - Morning Shift", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(120, body.RootElement.GetProperty("totalSeats").GetInt32());
        Assert.Equal($"http://localhost/classes/{id}", response.Headers.Location?.ToString());

        // Identical names are allowed; persistence is checked using a separate DbContext.
        var second = await CreateClass("Class 6 - Morning Shift", 120);
        Assert.NotEqual(id, second.Id);
        await using var context = _database.CreateContext();
        var stored = await context.AdmissionClasses.AsNoTracking().SingleAsync(entity => entity.Id == id);
        Assert.Equal("Class 6 - Morning Shift", stored.Name);
        Assert.Equal(120, stored.TotalSeats);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
        Assert.Equal(2, await context.AdmissionClasses.CountAsync());

        using var getResponse = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var resource = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        AssertClass(resource.RootElement, id, "Class 6 - Morning Shift", 120, 0, 0, 120);
    }

    [PostgreSqlFact]
    public async Task List_EmptyDatabaseReturnsEmptyArrayInOneCommand()
    {
        _commands.Commands.Clear();
        using var response = await _client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
        Assert.Equal(0, body.RootElement.GetArrayLength());
        Assert.Single(_commands.Commands);
    }

    [PostgreSqlFact]
    public async Task Get_MissingClassReturnsProblemDetails404()
    {
        using var response = await _client.GetAsync("/classes/c_missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Class not found.", body.RootElement.GetProperty("title").GetString());
    }

    [PostgreSqlFact]
    public async Task Create_InvalidInputsReturn400AndDoNotPersist()
    {
        string[] invalidBodies =
        [
            "{}", "null", "{\"totalSeats\":10}",
            "{\"name\":null,\"totalSeats\":10}",
            "{\"name\":\"\",\"totalSeats\":10}",
            "{\"name\":\"   \",\"totalSeats\":10}",
            JsonSerializer.Serialize(new { name = new string('x', 161), totalSeats = 10 }),
            "{\"name\":\"Class 6\"}",
            "{\"name\":\"Class 6\",\"totalSeats\":0}",
            "{\"name\":\"Class 6\",\"totalSeats\":-1}",
            "{\"name\":\"Class 6\",\"totalSeats\":2147483648}",
            "{\"name\":\"Class 6\",\"totalSeats\":-2147483649}",
            "{\"name\":\"Class 6\",\"totalSeats\":1.5}",
            "{\"name\":\"Class 6\",\"totalSeats\":\"invalid\"}",
            "{"
        ];
        foreach (var json in invalidBodies)
        {
            using var response = await _client.PostAsync("/classes", new StringContent(json, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
            Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateObject());
        }

        await using var context = _database.CreateContext();
        Assert.Equal(0, await context.AdmissionClasses.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ListAndGet_CountOnlyUnexpiredHeldAndConfirmedUsingOneStatementEach()
    {
        var first = await CreateClass("Class 6 - Morning", 10);
        var second = await CreateClass("Class 6 - Day", 5);
        var empty = await CreateClass("Class 7 - Morning", 3);
        await using var context = _database.CreateContext();
        await InsertHold(context, first.Id, "00000000000000001", "HELD", 1);
        await InsertHold(context, first.Id, "00000000000000002", "HELD", 1);
        await InsertHold(context, first.Id, "00000000000000003", "HELD", -1);
        await InsertHold(context, first.Id, "00000000000000004", "CONFIRMED", -1);
        await InsertHold(context, first.Id, "00000000000000005", "EXPIRED", 1);
        await InsertHold(context, first.Id, "00000000000000006", "CANCELLED", 1);
        await InsertHold(context, second.Id, "00000000000000001", "CONFIRMED", -1);

        _commands.Commands.Clear();
        using var listResponse = await _client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, list.RootElement.GetArrayLength());
        var rows = list.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString()!);
        AssertClass(rows[first.Id], first.Id, first.Name, 10, 2, 1, 7);
        AssertClass(rows[second.Id], second.Id, second.Name, 5, 0, 1, 4);
        AssertClass(rows[empty.Id], empty.Id, empty.Name, 3, 0, 0, 3);
        AssertStatisticsCommand();

        foreach (var admissionClass in new[] { first, second, empty })
        {
            _commands.Commands.Clear();
            using var response = await _client.GetAsync($"/classes/{admissionClass.Id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(rows[admissionClass.Id].ToString(), body.RootElement.ToString());
            AssertStatisticsCommand();
        }

        var expiredButHeld = await context.AdmissionHolds.AsNoTracking().SingleAsync(
            hold => hold.ClassId == first.Id && hold.StudentId == "00000000000000003");
        Assert.Equal(HoldStatus.Held, expiredButHeld.Status);
    }

    private async Task<CreateClassResponse> CreateClass(string name, int totalSeats)
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateClassResponse>())!;
    }

    private static Task<int> InsertHold(ApplicationDbContext context, string classId, string studentId, string status, int expiryHours) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO admission_holds (id, class_id, student_id, status, created_at, expires_at)
            VALUES ({$"h_{Guid.NewGuid():N}"}, {classId}, {studentId}, {status},
                    statement_timestamp() - interval '2 hours', statement_timestamp() + {expiryHours} * interval '1 hour')
            """);

    private void AssertStatisticsCommand()
    {
        var sql = Assert.Single(_commands.Commands);
        Assert.Contains("statement_timestamp()", sql);
        Assert.Contains("GROUP BY", sql);
    }

    private static void AssertProperties(JsonElement body, params string[] properties) =>
        Assert.Equal(properties.Order(), body.EnumerateObject().Select(property => property.Name).Order());

    private static void AssertClass(JsonElement body, string id, string name, int total, int held, int confirmed, int available)
    {
        AssertProperties(body, "id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats");
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(total, body.GetProperty("totalSeats").GetInt32());
        Assert.Equal(held, body.GetProperty("heldSeats").GetInt32());
        Assert.Equal(confirmed, body.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(available, body.GetProperty("availableSeats").GetInt32());
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
