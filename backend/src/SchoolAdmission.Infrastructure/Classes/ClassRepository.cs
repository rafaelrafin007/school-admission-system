using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Application.Classes.Interfaces;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.Classes;

public sealed class ClassRepository(ApplicationDbContext context) : IClassRepository
{
    // One statement provides a single snapshot and a stable database-time reference for all classes.
    private const string StatisticsSql = """
        SELECT seats.id AS "Id", seats.name AS "Name", seats.total_seats AS "TotalSeats",
               seats.held_seats AS "HeldSeats", seats.confirmed_seats AS "ConfirmedSeats",
               seats.total_seats - seats.held_seats - seats.confirmed_seats AS "AvailableSeats"
        FROM (
            SELECT c.id, c.name, c.total_seats,
                   (COUNT(h.id) FILTER (
                       WHERE h.status = 'HELD' AND h.expires_at > statement_timestamp()))::integer AS held_seats,
                   (COUNT(h.id) FILTER (WHERE h.status = 'CONFIRMED'))::integer AS confirmed_seats
            FROM admission_classes AS c
            LEFT JOIN admission_holds AS h ON h.class_id = c.id
                AND h.status IN ('HELD', 'CONFIRMED')
            GROUP BY c.id, c.name, c.total_seats
        ) AS seats
        """;

    public async Task AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken)
    {
        context.AdmissionClasses.Add(admissionClass);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        await StatisticsQuery().OrderBy(admissionClass => admissionClass.Name)
            .ThenBy(admissionClass => admissionClass.Id).ToListAsync(cancellationToken);

    public Task<ClassResponse?> GetByIdAsync(string classId, CancellationToken cancellationToken) =>
        StatisticsQuery().SingleOrDefaultAsync(admissionClass => admissionClass.Id == classId, cancellationToken);

    // Unmapped DTO queries are inherently untracked; LINQ filters are parameterized by EF Core.
    private IQueryable<ClassResponse> StatisticsQuery() => context.Database.SqlQueryRaw<ClassResponse>(StatisticsSql);
}
