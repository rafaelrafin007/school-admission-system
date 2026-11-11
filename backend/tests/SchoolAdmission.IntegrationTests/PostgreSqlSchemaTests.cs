using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

public sealed class PostgreSqlSchemaTests(PostgreSqlTestDatabase database) : IClassFixture<PostgreSqlTestDatabase>
{
    private static readonly DateTime Timestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    [PostgreSqlFact]
    public async Task Migration_CreatesExpectedTablesConstraintsAndIndexes()
    {
        await using var context = database.CreateContext();
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = current_schema() ORDER BY tablename").ToListAsync();
        Assert.Equal(new[] { "__EFMigrationsHistory", "admission_classes", "admission_holds" }, tables);

        var constraints = await context.Database.SqlQueryRaw<string>(
            "SELECT conname AS \"Value\" FROM pg_constraint WHERE connamespace = current_schema()::regnamespace").ToListAsync();
        foreach (var name in new[] { "pk_admission_classes", "pk_admission_holds", "ck_admission_classes_total_seats_positive",
                     "ck_admission_holds_status", "fk_admission_holds_admission_classes_class_id" })
        {
            Assert.Contains(name, constraints);
        }

        var indexes = await context.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'admission_holds'").ToListAsync();
        Assert.Equal(new[] { "pk_admission_holds", "ux_admission_holds_class_student_active",
                "ix_admission_holds_class_status_expires_at", "ix_admission_holds_student_created_at",
                "ix_admission_holds_held_expires_at" }.Order(), indexes.Order());
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgreSqlFact]
    public async Task Entities_RoundTripUppercaseStatusesLeadingZerosAndUtcTimestamps()
    {
        await using var context = database.CreateContext();
        var admissionClass = await InsertClass(context);
        foreach (var status in Enum.GetValues<HoldStatus>())
        {
            var studentId = ((int)status).ToString("D17");
            var hold = new AdmissionHold(NewId("h"), admissionClass.Id, studentId, Timestamp, Timestamp.AddMinutes(10));
            context.AdmissionHolds.Add(hold);
            context.Entry(hold).Property(entity => entity.Status).CurrentValue = status;
            if (status == HoldStatus.Confirmed)
                context.Entry(hold).Property(entity => entity.ConfirmedAt).CurrentValue = Timestamp.AddMinutes(1);
            if (status == HoldStatus.Cancelled)
                context.Entry(hold).Property(entity => entity.CancelledAt).CurrentValue = Timestamp.AddMinutes(1);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var stored = await context.AdmissionHolds.SingleAsync(entity => entity.Id == hold.Id);
            Assert.Equal(studentId, stored.StudentId);
            Assert.Equal(status, stored.Status);
            Assert.Equal(Timestamp, stored.CreatedAt);
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, stored.ExpiresAt.Kind);
            Assert.Equal(hold.ConfirmedAt, stored.ConfirmedAt);
            Assert.Equal(hold.CancelledAt, stored.CancelledAt);
            var rawStatus = await context.Database.SqlQuery<string>(
                $"SELECT status AS \"Value\" FROM admission_holds WHERE id = {hold.Id}").SingleAsync();
            Assert.Equal(status.ToString().ToUpperInvariant(), rawStatus);
        }
    }

    [PostgreSqlFact]
    public async Task Database_RejectsZeroAndNegativeSeatCapacity()
    {
        await using var context = database.CreateContext();
        foreach (var capacity in new[] { 0, -1 })
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO admission_classes (id, name, total_seats, created_at) VALUES ({NewId("c")}, {"Invalid capacity"}, {capacity}, {Timestamp})"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_admission_classes_total_seats_positive", exception.ConstraintName);
        }
    }

    [PostgreSqlFact]
    public async Task Database_RejectsInvalidStatus()
    {
        await using var context = database.CreateContext();
        var admissionClass = await InsertClass(context);
        foreach (var status in new[] { "held", "UNKNOWN" })
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertHold(context, admissionClass.Id, "00152691234567890", status));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_admission_holds_status", exception.ConstraintName);
        }
    }

    [PostgreSqlFact]
    public async Task ForeignKey_RejectsMissingClassAndRestrictsDeletion()
    {
        await using var context = database.CreateContext();
        var missingClass = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertHold(context, NewId("c"), "00152691234567890", "HELD"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, missingClass.SqlState);

        var admissionClass = await InsertClass(context);
        await InsertHold(context, admissionClass.Id, "00152691234567890", "HELD");
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM admission_classes WHERE id = {admissionClass.Id}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        Assert.Equal("fk_admission_holds_admission_classes_class_id", deletion.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task ActiveUniqueIndex_RequiresStatusChangeEvenAfterDeadline()
    {
        await using var context = database.CreateContext();
        var admissionClass = await InsertClass(context);
        const string studentId = "00152691234567890";
        await InsertHold(context, admissionClass.Id, studentId, "HELD");
        foreach (var status in new[] { "HELD", "CONFIRMED" })
        {
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertHold(context, admissionClass.Id, studentId, status));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
            Assert.Equal("ux_admission_holds_class_student_active", duplicate.ConstraintName);
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE admission_holds SET status = 'EXPIRED' WHERE class_id = {admissionClass.Id}");
        await InsertHold(context, admissionClass.Id, studentId, "CONFIRMED");
        var confirmedDuplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertHold(context, admissionClass.Id, studentId, "HELD"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, confirmedDuplicate.SqlState);

        await InsertHold(context, admissionClass.Id, studentId, "EXPIRED");
        await InsertHold(context, admissionClass.Id, studentId, "CANCELLED");
        var otherClass = await InsertClass(context);
        await InsertHold(context, otherClass.Id, studentId, "HELD");
    }

    private static async Task<AdmissionClass> InsertClass(ApplicationDbContext context)
    {
        var admissionClass = new AdmissionClass(NewId("c"), "Class 6 - Morning", 10, Timestamp);
        context.AdmissionClasses.Add(admissionClass);
        await context.SaveChangesAsync();
        return admissionClass;
    }

    private static Task<int> InsertHold(ApplicationDbContext context, string classId, string studentId, string status) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO admission_holds (id, class_id, student_id, status, created_at, expires_at) VALUES ({NewId("h")}, {classId}, {studentId}, {status}, {Timestamp}, {Timestamp.AddMinutes(10)})");
}
