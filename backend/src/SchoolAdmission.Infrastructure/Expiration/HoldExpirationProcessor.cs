using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.Expiration;

public sealed class HoldExpirationProcessor(ApplicationDbContext context, HoldExpirationSettings settings,
    ILogger<HoldExpirationProcessor> logger) : IHoldExpirationProcessor
{
    public async Task<HoldExpirationBatchResult> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        // Discovery takes no row locks. Every candidate is rechecked under its class lock.
        var classIds = await context.Database.SqlQuery<string>($"""
            SELECT class_id AS "Value" FROM admission_holds
            WHERE status = 'HELD' AND expires_at <= statement_timestamp()
            GROUP BY class_id ORDER BY MIN(expires_at), class_id
            LIMIT {settings.BatchSize}
            """).ToListAsync(cancellationToken);

        long expiredHolds = 0;
        foreach (var classId in classIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                expiredHolds += await ProcessClassAsync(classId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Do not log provider messages, which may contain connection or row data.
                logger.LogError("Expiration failed for class {ClassId} ({ErrorType}).", classId, exception.GetType().Name);
                throw;
            }
        }
        return new HoldExpirationBatchResult(classIds.Count, expiredHolds);
    }

    public async Task<int> ProcessClassAsync(string classId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var lockedIds = await context.Database.SqlQuery<string>($"""
            SELECT id AS "Value" FROM admission_classes WHERE id = {classId} FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (lockedIds.Count == 0) return 0;

        var currentTime = await context.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        // The predicate is AdmissionHold.Expire's rule, applied in PostgreSQL without loading holds.
        // UPDATE acquires hold locks only after the class lock; it changes no history timestamps.
        var expired = await context.AdmissionHolds
            .Where(hold => hold.ClassId == classId && hold.Status == HoldStatus.Held && hold.ExpiresAt <= currentTime)
            .ExecuteUpdateAsync(setters => setters.SetProperty(hold => hold.Status, HoldStatus.Expired), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expired;
    }
}
