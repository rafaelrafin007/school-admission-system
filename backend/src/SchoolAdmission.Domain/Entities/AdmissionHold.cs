using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Domain.Entities;

public sealed class AdmissionHold
{
    private AdmissionHold() { }

    public AdmissionHold(string id, string classId, string studentId, DateTime createdAt, DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);
        if (!id.StartsWith("h_", StringComparison.Ordinal) || id.Length <= 2 || id.Length > 40)
        {
            throw new ArgumentException("Hold ID must start with h_ and contain 3 to 40 characters.", nameof(id));
        }
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be UTC.", nameof(createdAt));
        }
        if (expiresAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be UTC.", nameof(expiresAt));
        }
        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Expiration must be later than creation.", nameof(expiresAt));
        }

        Id = id;
        ClassId = classId;
        StudentId = studentId;
        Status = HoldStatus.Held;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string Id { get; private set; } = string.Empty;
    public string ClassId { get; private set; } = string.Empty;
    public string StudentId { get; private set; } = string.Empty;
    public HoldStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
}
