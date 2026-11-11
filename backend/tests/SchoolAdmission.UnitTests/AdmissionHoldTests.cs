using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.UnitTests;

public sealed class AdmissionHoldTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Construction_PreservesIdentityAndStartsHeldWithPositiveLifetime()
    {
        var hold = new AdmissionHold("h_example", "c_example", "00student", CreatedAt, CreatedAt.AddSeconds(600));
        Assert.Equal("h_example", hold.Id);
        Assert.Equal("c_example", hold.ClassId);
        Assert.Equal("00student", hold.StudentId);
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.Equal(CreatedAt, hold.CreatedAt);
        Assert.Equal(CreatedAt.AddSeconds(600), hold.ExpiresAt);
        Assert.Null(hold.ConfirmedAt);
        Assert.Null(hold.CancelledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construction_RejectsNonpositiveLifetime(int seconds) =>
        Assert.Throws<ArgumentException>(() => new AdmissionHold("h_1", "c_1", "001", CreatedAt, CreatedAt.AddSeconds(seconds)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("h_")]
    [InlineData("incorrect_prefix")]
    [InlineData("h_12345678901234567890123456789012345678901")]
    public void Construction_RejectsInvalidHoldId(string? id) =>
        Assert.ThrowsAny<ArgumentException>(() => new AdmissionHold(id!, "c_1", "001", CreatedAt, CreatedAt.AddSeconds(1)));

    [Theory]
    [InlineData("", "001")]
    [InlineData("c_1", " ")]
    public void Construction_RejectsBlankClassOrStudent(string classId, string studentId) =>
        Assert.Throws<ArgumentException>(() => new AdmissionHold("h_1", classId, studentId, CreatedAt, CreatedAt.AddSeconds(1)));
}
