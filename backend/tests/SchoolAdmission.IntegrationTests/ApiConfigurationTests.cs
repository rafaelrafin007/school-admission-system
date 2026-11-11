using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolAdmission.Api;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.IntegrationTests;

public sealed class ApiConfigurationTests
{
    [Fact]
    public void ApiStartup_RejectsMissingDatabaseConfiguration()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:DefaultConnection", ""));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    [Theory]
    [InlineData(HoldStatus.Held, "HELD")]
    [InlineData(HoldStatus.Confirmed, "CONFIRMED")]
    [InlineData(HoldStatus.Expired, "EXPIRED")]
    [InlineData(HoldStatus.Cancelled, "CANCELLED")]
    public void ApiJsonOptions_SerializeStatusAsUppercase(HoldStatus status, string expected)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=json_only;Username=unused");
            builder.ConfigureServices(services => services.RemoveHoldExpirationWorker());
            builder.UseSetting("SCHOOL_ADMISSION_SKIP_MIGRATIONS", "true");
        });
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        Assert.Equal($"\"{expected}\"", JsonSerializer.Serialize(status, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HoldStatus>("0", options));
    }
}
