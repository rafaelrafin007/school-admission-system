using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolAdmission.Api;
using SchoolAdmission.Application.Classes.DTOs;
using SchoolAdmission.Application.Classes.Interfaces;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.IntegrationTests;

public sealed class ClassApiErrorTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedFailure_ReturnsGenericProblemWithoutExceptionDetails(string environment)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=error_test;Username=unused");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IClassRepository>();
                services.AddScoped<IClassRepository, FailingRepository>();
            });
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/classes");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected server error occurred.", body.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Internal database diagnostic", json);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailingRepository : IClassRepository
    {
        public Task AddAsync(AdmissionClass admissionClass, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Internal database diagnostic");

        public Task<IReadOnlyList<ClassResponse>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Internal database diagnostic");

        public Task<ClassResponse?> GetByIdAsync(string classId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Internal database diagnostic");
    }
}
