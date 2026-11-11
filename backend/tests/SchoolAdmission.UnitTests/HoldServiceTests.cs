using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.Interfaces;
using SchoolAdmission.Application.Holds.DTOs;

namespace SchoolAdmission.UnitTests;

public sealed class HoldServiceTests
{
    [Theory]
    [InlineData("  00152691234567890  ", "00152691234567890")]
    [InlineData("a", "a")]
    [InlineData("  student-A  ", "student-A")]
    public async Task Create_NormalizesTextIdentityAndForwardsCancellation(string input, string expected)
    {
        var repository = new RecordingRepository();
        using var cancellation = new CancellationTokenSource();
        var result = await new HoldService(repository).CreateAsync("c_example", input, cancellation.Token);
        Assert.Equal(expected, repository.StudentId);
        Assert.Equal("c_example", repository.ClassId);
        Assert.Equal(cancellation.Token, repository.CancellationToken);
        Assert.Equal(CreateHoldOutcome.DuplicateApplication, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("123456789012345678")]
    public async Task Create_RejectsInvalidStudentBeforePersistence(string? studentId)
    {
        var repository = new RecordingRepository();
        await Assert.ThrowsAsync<ValidationException>(() => new HoldService(repository).CreateAsync("c_1", studentId, CancellationToken.None));
        Assert.Null(repository.StudentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("c_")]
    [InlineData("c_12345678901234567890123456789012345678901")]
    public async Task Create_InvalidClassReturnsNotFoundWithoutPersistence(string classId)
    {
        var repository = new RecordingRepository();
        var result = await new HoldService(repository).CreateAsync(classId, "001", CancellationToken.None);
        Assert.Equal(CreateHoldOutcome.ClassNotFound, result.Outcome);
        Assert.Null(repository.StudentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Settings_RejectNonpositiveDuration(int duration) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoldSettings(duration));

    [Fact]
    public void Settings_DefaultToTenMinutes() => Assert.Equal(600, new HoldSettings().DurationSeconds);

    private sealed class RecordingRepository : IHoldRepository
    {
        public Task<HoldDetailsResponse?> GetByIdAsync(string holdId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyHoldResponse>> GetForStudentAsync(string studentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HoldTransitionOutcome> ConfirmAsync(string holdId, string studentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HoldTransitionOutcome> CancelAsync(string holdId, string studentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public string? StudentId { get; private set; }
        public string? ClassId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<CreateHoldResult> CreateAsync(string classId, string studentId, CancellationToken cancellationToken)
        {
            ClassId = classId;
            StudentId = studentId;
            CancellationToken = cancellationToken;
            return Task.FromResult(new CreateHoldResult(CreateHoldOutcome.DuplicateApplication));
        }
    }
}
