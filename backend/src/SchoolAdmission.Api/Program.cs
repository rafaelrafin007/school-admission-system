using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddScoped<ClassService>();
        builder.Services.AddScoped<HoldService>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)));

        var app = builder.Build();
        var skipMigrations = string.Equals(
            builder.Configuration["SCHOOL_ADMISSION_SKIP_MIGRATIONS"], "true", StringComparison.OrdinalIgnoreCase);

        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            if (skipMigrations)
            {
                logger.LogDebug("Database migration skipped: SCHOOL_ADMISSION_SKIP_MIGRATIONS is set.");
            }
            else
            {
                context.Database.Migrate();
            }
        }

        app.UseExceptionHandler();
        app.MapControllers();

        app.Run();
    }
}
