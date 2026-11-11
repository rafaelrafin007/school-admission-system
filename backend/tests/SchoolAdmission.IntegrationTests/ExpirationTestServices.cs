using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolAdmission.Infrastructure.Expiration;

namespace SchoolAdmission.IntegrationTests;

internal static class ExpirationTestServices
{
    // API-only tests intentionally exercise lazy expiration without a competing hosted cycle.
    public static IServiceCollection RemoveHoldExpirationWorker(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(HoldExpirationWorker)).ToArray())
            services.Remove(descriptor);
        return services;
    }
}
