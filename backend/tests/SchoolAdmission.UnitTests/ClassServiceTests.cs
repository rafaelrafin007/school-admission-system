using System.ComponentModel.DataAnnotations;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Application.Classes.Interfaces;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.UnitTests;

public sealed class ClassServiceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(int.MaxValue)]
    public async Task Create_PersistsNormalizedClassWithUniqueIdAndUtcTimestamp(int totalSeats)
    {
        var repository = new RecordingRepository();
        var service = new ClassService(repository);
        using var cancellation = new CancellationTokenSource();
        var request = new CreateClassRequest { Name = "  Class 6 - Morning Shift  ", TotalSeats = totalSeats };

        var response = await service.CreateAsync(request, cancellation.Token);
        var second = await service.CreateAsync(request, cancellation.Token);

        Assert.StartsWith("c_", response.Id);
        Assert.True(Guid.TryParseExact(response.Id[2..], "N", out _));
        Assert.NotEqual(response.Id, second.Id);
        Assert.Equal("Class 6 - Morning Shift", response.Name);
        Assert.Equal(totalSeats, response.TotalSeats);
        Assert.Equal(2, repository.Added.Count);
        Assert.Equal(response.Id, repository.Added[0].Id);
        Assert.Equal(DateTimeKind.Utc, repository.Added[0].CreatedAt.Kind);
        Assert.Equal(cancellation.Token, repository.LastCancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public async Task Create_RejectsMissingOrBlankName(string? name)
    {
        await AssertInvalid(new CreateClassRequest { Name = name, TotalSeats = 10 });
    }

    [Fact]
    public async Task Create_RejectsNameLongerThanDatabaseLimit() =>
        await AssertInvalid(new CreateClassRequest { Name = new string('x', 161), TotalSeats = 10 });

    [Fact]
    public async Task Create_AcceptsNameAtDatabaseLimit()
    {
        var service = new ClassService(new RecordingRepository());
        var name = new string('x', 160);
        var response = await service.CreateAsync(new CreateClassRequest { Name = name, TotalSeats = 1 }, CancellationToken.None);
        Assert.Equal(name, response.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Create_RejectsNonpositiveCapacity(int totalSeats) =>
        await AssertInvalid(new CreateClassRequest { Name = "Class 6", TotalSeats = totalSeats });

    private static async Task AssertInvalid(CreateClassRequest request)
    {
        var repository = new RecordingRepository();
        await Assert.ThrowsAsync<ValidationException>(() => new ClassService(repository).CreateAsync(request, CancellationToken.None));
        Assert.Empty(repository.Added);
    }

    private sealed class RecordingRepository : IClassRepository
    {
        public List<AdmissionClass> Added { get; } = [];
        public CancellationToken LastCancellationToken { get; private set; }

        public Task AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken)
        {
            Added.Add(admissionClass);
            LastCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ClassResponse>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ClassResponse?> GetByIdAsync(string classId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
