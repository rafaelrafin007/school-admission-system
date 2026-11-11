using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolAdmission.Api;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Expiration;

namespace SchoolAdmission.IntegrationTests;

public sealed class HoldExpirationConfigurationTests
{
    [Fact]
    public void MissingConfigurationUsesFiveSecondsAndFiftyClasses()
    {
        var settings = HoldExpirationSettings.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Equal(5, settings.IntervalSeconds);
        Assert.Equal(50, settings.BatchSize);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(17, 23)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void CustomConfigurationIsRespected(int interval, int batch)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HOLD_EXPIRATION_INTERVAL_SECONDS"] = interval.ToString(),
            ["HOLD_EXPIRATION_BATCH_SIZE"] = batch.ToString()
        }).Build();
        var settings = HoldExpirationSettings.FromConfiguration(configuration);
        Assert.Equal(interval, settings.IntervalSeconds);
        Assert.Equal(batch, settings.BatchSize);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    public void SettingsRejectNonpositiveValues(int interval, int batch) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoldExpirationSettings(interval, batch));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void InvalidConfigurationFailsApiStartupClearly(string value)
    {
        foreach (var key in new[] { "HOLD_EXPIRATION_INTERVAL_SECONDS", "HOLD_EXPIRATION_BATCH_SIZE" })
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=settings_only;Username=unused");
                builder.UseSetting(key, value);
                builder.ConfigureServices(services => services.RemoveHoldExpirationWorker());
            });
            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Equal($"{key} must be a positive 32-bit integer.", exception.Message);
        }
    }

    [Fact]
    public void InfrastructureRegistersExactlyOneWorkerAndScopedProcessor()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=settings_only;Username=unused" }).Build();
        var services = new ServiceCollection().AddInfrastructure(configuration);
        var worker = Assert.Single(services, service => service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(HoldExpirationWorker));
        Assert.Equal(ServiceLifetime.Singleton, worker.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, service =>
            service.ServiceType == typeof(IHoldExpirationProcessor)).Lifetime);
    }
}
