using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolAdmission.Api;

namespace SchoolAdmission.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        using var configuredFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.RemoveHoldExpirationWorker());
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=school_admission_health_test;Username=unused");
            builder.UseSetting("SCHOOL_ADMISSION_SKIP_MIGRATIONS", "true");
        });
        using var client = configuredFactory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
    }
}
