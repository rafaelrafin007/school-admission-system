using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api;
using SchoolAdmission.Application.Holds;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldConfigurationTests
{
    [Theory]
    [InlineData(null, 600)]
    [InlineData("45", 45)]
    [InlineData("1", 1)]
    public void Startup_UsesDefaultOrConfiguredPositiveDuration(string? value, int expected)
    {
        using var factory = CreateFactory(value);
        Assert.Equal(expected, factory.Services.GetRequiredService<HoldSettings>().DurationSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void Startup_RejectsInvalidDurationClearly(string value)
    {
        using var factory = CreateFactory(value);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Equal("HOLD_DURATION_SECONDS must be a positive 32-bit integer.", exception.Message);
    }

    private static WebApplicationFactory<Program> CreateFactory(string? value) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.RemoveHoldExpirationWorker());
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=configuration_only;Username=unused");
            builder.UseSetting("SCHOOL_ADMISSION_SKIP_MIGRATIONS", "true");
            if (value is not null)
                builder.UseSetting("HOLD_DURATION_SECONDS", value);
        });
}
