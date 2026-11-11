using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.UnitTests;

public sealed class EntityTimestampTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Entities_RejectNonUtcTimestamps(DateTimeKind kind)
    {
        var invalid = new DateTime(2026, 11, 11, 0, 0, 0, kind);
        var utc = DateTime.SpecifyKind(invalid, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new AdmissionClass("c_1", "Class 6 - Morning", 10, invalid));
        var invalidCreation = Assert.Throws<ArgumentException>(() =>
            new AdmissionHold("h_1", "c_1", "00152691234567890", invalid, utc.AddSeconds(600)));
        Assert.Equal("createdAt", invalidCreation.ParamName);
        var invalidExpiration = Assert.Throws<ArgumentException>(() =>
            new AdmissionHold("h_1", "c_1", "00152691234567890", utc, invalid.AddSeconds(600)));
        Assert.Equal("expiresAt", invalidExpiration.ParamName);
    }
}
