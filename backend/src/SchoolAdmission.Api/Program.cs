using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolAdmission.Infrastructure;

namespace SchoolAdmission.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)));

        var app = builder.Build();

        app.MapControllers();

        app.Run();
    }
}
