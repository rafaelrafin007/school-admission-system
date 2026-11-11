using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Api;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldApiTestHost : IAsyncLifetime
{
    public PostgreSqlTestDatabase Database { get; } = new();
    public string ApplicationName { get; } = $"hold-tests-{Guid.NewGuid():N}";
    public string? DurationSeconds { get; init; }
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public InsertFailureInterceptor InsertFailure { get; } = new();

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlTestDatabase.ConnectionVariable)))
            return;

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Database.ConnectionString)
                { ApplicationName = ApplicationName, MaxPoolSize = 20 };
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                if (DurationSeconds is not null)
                    builder.UseSetting("HOLD_DURATION_SECONDS", DurationSeconds);
                builder.ConfigureServices(services => services.AddDbContext<ApplicationDbContext>(
                    options => options.AddInterceptors(InsertFailure)));
            });
            Client = Factory.CreateClient();
        }
        catch (Exception originalException)
        {
            try { await DisposeAsync(); }
            catch (Exception cleanupException) { originalException.Data["CleanupException"] = cleanupException; }
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            Client?.Dispose();
            if (Factory is not null)
                await Factory.DisposeAsync();
        }
        finally { await Database.DisposeAsync(); }
    }

    public async Task<CreateClassResponse> CreateClassAsync(int capacity)
    {
        using var response = await Client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning", totalSeats = capacity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateClassResponse>())!;
    }

    public async Task<HttpResponseMessage> ReserveAsync(string classId, string? studentId, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        if (studentId is not null)
            request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return await (client ?? Client).SendAsync(request);
    }

    public async Task<string> SeedHoldAsync(string classId, string studentId, string status, int expirySeconds)
    {
        var holdId = $"h_{Guid.NewGuid():N}";
        await using var context = Database.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO admission_holds (id, class_id, student_id, status, created_at, expires_at, confirmed_at, cancelled_at)
            VALUES ({holdId}, {classId}, {studentId}, {status}, clock_timestamp() - interval '2 days',
                clock_timestamp() + {expirySeconds} * interval '1 second',
                CASE WHEN {status} = 'CONFIRMED' THEN clock_timestamp() END,
                CASE WHEN {status} = 'CANCELLED' THEN clock_timestamp() END)
            """);
        return holdId;
    }

    public async Task WaitForBlockedRequestsAsync(int minimum)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var observer = Database.CreateContext();
        while (true)
        {
            var waiting = await observer.Database.SqlQuery<int>($"""
                SELECT count(*)::integer AS "Value" FROM pg_stat_activity
                WHERE application_name = {ApplicationName} AND wait_event_type = 'Lock'
                  AND query LIKE '%FOR UPDATE%'
                """).SingleAsync(timeout.Token);
            if (waiting >= minimum)
                return;
            await Task.Delay(20, timeout.Token);
        }
    }

    public sealed class InsertFailureInterceptor : DbCommandInterceptor
    {
        public bool FailNextHoldInsert { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextHoldInsert && command.CommandText.Contains("INSERT", StringComparison.Ordinal) &&
                command.CommandText.Contains("admission_holds", StringComparison.Ordinal))
            {
                FailNextHoldInsert = false;
                throw new InvalidOperationException("Injected internal hold insert failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
