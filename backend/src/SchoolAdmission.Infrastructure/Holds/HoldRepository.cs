using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.DTOs;
using SchoolAdmission.Application.Holds.Interfaces;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.Holds;

public sealed class HoldRepository(ApplicationDbContext context, HoldSettings settings) : IHoldRepository
{
    public Task<HoldDetailsResponse?> GetByIdAsync(string holdId, CancellationToken cancellationToken) =>
        context.Database.SqlQuery<HoldDetailsResponse>($"""
            SELECT id AS "HoldId", class_id AS "ClassId", expires_at AS "ExpiresAt",
                   CASE WHEN status = 'HELD' AND expires_at <= statement_timestamp()
                        THEN 'EXPIRED' ELSE status END AS "Status"
            FROM admission_holds WHERE id = {holdId}
            """).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MyHoldResponse>> GetForStudentAsync(string studentId, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<MyHoldResponse>($"""
            SELECT h.id AS "HoldId", h.class_id AS "ClassId", c.name AS "ClassName", h.expires_at AS "ExpiresAt",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= statement_timestamp()
                        THEN 'EXPIRED' ELSE h.status END AS "Status"
            FROM admission_holds AS h JOIN admission_classes AS c ON c.id = h.class_id
            WHERE h.student_id = {studentId}
            ORDER BY h.created_at DESC, h.id DESC
            """).ToListAsync(cancellationToken);

    public Task<HoldTransitionOutcome> ConfirmAsync(string holdId, string studentId, CancellationToken cancellationToken) =>
        TransitionAsync(holdId, studentId, confirm: true, cancellationToken);

    public Task<HoldTransitionOutcome> CancelAsync(string holdId, string studentId, CancellationToken cancellationToken) =>
        TransitionAsync(holdId, studentId, confirm: false, cancellationToken);

    private async Task<HoldTransitionOutcome> TransitionAsync(string holdId, string studentId, bool confirm, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Resolve the immutable class ID without taking a hold row lock first.
        var classId = await context.AdmissionHolds.Where(hold => hold.Id == holdId)
            .Select(hold => hold.ClassId).SingleOrDefaultAsync(cancellationToken);
        if (classId is null) return HoldTransitionOutcome.NotFound;

        var classes = await context.AdmissionClasses.FromSql($"SELECT * FROM admission_classes WHERE id = {classId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (classes.Count == 0) return HoldTransitionOutcome.NotFound;

        var holds = await context.AdmissionHolds.FromSql($"SELECT * FROM admission_holds WHERE id = {holdId} AND class_id = {classId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var hold = holds.SingleOrDefault();
        if (hold is null) return HoldTransitionOutcome.NotFound;
        var currentTime = await context.Database.SqlQueryRaw<DateTime>("SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (hold.StudentId != studentId) return HoldTransitionOutcome.Forbidden;

        // Expiration is committed even though the requested transition returns a conflict.
        var expired = hold.Expire(currentTime);
        var succeeded = !expired && (confirm ? hold.Confirm(currentTime) : hold.Cancel(currentTime));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return succeeded ? HoldTransitionOutcome.Succeeded : HoldTransitionOutcome.InvalidState;
    }

    public async Task<CreateHoldResult> CreateAsync(string classId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Acquire the class lock before reading time or occupancy. All future hold writers must use this order.
        var classes = await context.AdmissionClasses.FromSql($"""
            SELECT id, name, total_seats, created_at
            FROM admission_classes WHERE id = {classId} FOR UPDATE
            """).AsNoTracking().ToListAsync(cancellationToken);
        var admissionClass = classes.SingleOrDefault();
        if (admissionClass is null)
            return new CreateHoldResult(CreateHoldOutcome.ClassNotFound);

        var currentTime = await context.Database.SqlQueryRaw<DateTime>(
            "SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);

        await context.AdmissionHolds
            .Where(hold => hold.ClassId == classId && hold.Status == HoldStatus.Held && hold.ExpiresAt <= currentTime)
            .ExecuteUpdateAsync(setters => setters.SetProperty(hold => hold.Status, HoldStatus.Expired), cancellationToken);

        // Separate post-lock statements see preceding reservations committed while this transaction waited.
        var occupiedHolds = context.AdmissionHolds.Where(hold => hold.ClassId == classId &&
            (hold.Status == HoldStatus.Confirmed || (hold.Status == HoldStatus.Held && hold.ExpiresAt > currentTime)));
        if (await occupiedHolds.AnyAsync(hold => hold.StudentId == studentId, cancellationToken))
            return new CreateHoldResult(CreateHoldOutcome.DuplicateApplication);

        if (await occupiedHolds.LongCountAsync(cancellationToken) >= admissionClass.TotalSeats)
            return new CreateHoldResult(CreateHoldOutcome.ClassFull);

        var hold = new AdmissionHold($"h_{Guid.NewGuid():N}", classId, studentId,
            currentTime, currentTime.AddSeconds(settings.DurationSeconds));
        context.AdmissionHolds.Add(hold);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_admission_holds_class_student_active" })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.Entry(hold).State = EntityState.Detached;
            return new CreateHoldResult(CreateHoldOutcome.DuplicateApplication);
        }

        return new CreateHoldResult(CreateHoldOutcome.Created,
            new CreateHoldResponse(hold.Id, hold.ClassId, hold.StudentId, hold.Status, hold.ExpiresAt));
    }
}
