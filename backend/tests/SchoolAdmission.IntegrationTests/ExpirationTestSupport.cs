using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SchoolAdmission.Infrastructure.Expiration;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

internal static class ExpirationTestSupport
{
    public static ApplicationDbContext CreateContext(HoldApiTestHost host, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(host.Database.ConnectionString)
        { ApplicationName = host.ApplicationName }.ConnectionString).AddInterceptors(interceptors).Options);

    public static HoldExpirationProcessor Processor(ApplicationDbContext context, int batchSize = 50) =>
        new(context, new HoldExpirationSettings(batchSize: batchSize), NullLogger<HoldExpirationProcessor>.Instance);

    public static async Task<HoldExpirationBatchResult> ProcessBatch(HoldApiTestHost host, int batchSize = 50, CancellationToken token = default)
    {
        await using var context = CreateContext(host);
        return await Processor(context, batchSize).ProcessBatchAsync(token);
    }

    public static async Task<int> ProcessClass(HoldApiTestHost host, string classId, CancellationToken token = default)
    {
        await using var context = CreateContext(host);
        return await Processor(context).ProcessClassAsync(classId, token);
    }

    public static async Task LockClass(ApplicationDbContext context, string classId) =>
        await context.AdmissionClasses.FromSql($"SELECT * FROM admission_classes WHERE id = {classId} FOR UPDATE").AsNoTracking().ToListAsync();
}
