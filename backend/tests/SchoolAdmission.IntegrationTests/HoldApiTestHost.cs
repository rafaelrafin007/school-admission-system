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
    private NpgsqlDataSource? _dataSource;
    public PostgreSqlTestDatabase Database { get; } = new();
    public string ApplicationName { get; } = $"hold-tests-{Guid.NewGuid():N}";
    public string? DurationSeconds { get; init; }
    public bool EnableExpirationWorker { get; init; }
    public Action<IServiceCollection>? ConfigureServices { get; init; }
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public InsertFailureInterceptor InsertFailure { get; } = new();
    public CommitCancellationInterceptor CommitCancellation { get; } = new();

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlTestDatabase.ConnectionVariable)))
            return;

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Database.ConnectionString)
                { ApplicationName = ApplicationName, Pooling = true, MaxPoolSize = 20 };
            _dataSource = NpgsqlDataSource.Create(connection.ConnectionString);
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                if (DurationSeconds is not null)
                    builder.UseSetting("HOLD_DURATION_SECONDS", DurationSeconds);
                builder.ConfigureServices(services =>
                {
                    if (!EnableExpirationWorker) services.RemoveHoldExpirationWorker();
                    services.AddDbContext<ApplicationDbContext>(
                        options => options.UseNpgsql(_dataSource).AddInterceptors(InsertFailure, CommitCancellation));
                    ConfigureServices?.Invoke(services);
                });
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
        finally
        {
            try { if (_dataSource is not null) await _dataSource.DisposeAsync(); }
            finally { await Database.DisposeAsync(); }
        }
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

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? studentId = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (studentId is not null) request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        return await Client.SendAsync(request, cancellationToken);
    }

    public async Task WaitForNoTransactionsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var observer = Database.CreateContext();
        while (await observer.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM pg_stat_activity
            WHERE application_name = {ApplicationName} AND xact_start IS NOT NULL
            """).SingleAsync(timeout.Token) != 0)
            await Task.Delay(20, timeout.Token);
    }

    public sealed class CommitCancellationInterceptor : DbTransactionInterceptor
    {
        private int _armed;
        public TaskCompletionSource Reached { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm()
        {
            Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _armed, 1);
        }

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                Reached.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                throw new TimeoutException("The test did not cancel the request before commit.");
            }
            return result;
        }
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
