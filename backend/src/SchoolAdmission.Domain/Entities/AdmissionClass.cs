namespace SchoolAdmission.Domain.Entities;

public sealed class AdmissionClass
{
    private AdmissionClass() { }

    public AdmissionClass(string id, string name, int totalSeats, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalSeats);
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be UTC.", nameof(createdAt));
        }

        Id = id;
        Name = name;
        TotalSeats = totalSeats;
        CreatedAt = createdAt;
    }

    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int TotalSeats { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
