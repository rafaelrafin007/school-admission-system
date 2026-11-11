using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Classes.Interfaces;
using SchoolAdmission.Infrastructure.Classes;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.Interfaces;
using SchoolAdmission.Infrastructure.Holds;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database configuration is missing. Set ConnectionStrings__DefaultConnection.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        var durationValue = configuration["HOLD_DURATION_SECONDS"];
        var durationSeconds = 600;
        if (durationValue is not null &&
            (!int.TryParse(durationValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out durationSeconds) || durationSeconds <= 0))
        {
            throw new InvalidOperationException("HOLD_DURATION_SECONDS must be a positive 32-bit integer.");
        }

        services.AddSingleton(new HoldSettings(durationSeconds));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IHoldRepository, HoldRepository>();
        return services;
    }
}
