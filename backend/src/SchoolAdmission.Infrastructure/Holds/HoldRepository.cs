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
