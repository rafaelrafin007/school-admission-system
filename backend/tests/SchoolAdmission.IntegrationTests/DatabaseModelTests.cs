using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.IntegrationTests;

public sealed class DatabaseModelTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused").Options);

    [Fact]
    public void Model_HasExactlyTheRequestedTablesAndColumns()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        Assert.Equal(2, model.GetEntityTypes().Count());

        var admissionClass = model.FindEntityType(typeof(AdmissionClass))!;
        var hold = model.FindEntityType(typeof(AdmissionHold))!;
        Assert.Equal("admission_classes", admissionClass.GetTableName());
        Assert.Equal("admission_holds", hold.GetTableName());

        AssertColumns(admissionClass, new Dictionary<string, string>
        {
            ["id"] = "character varying(40)", ["name"] = "character varying(160)",
            ["total_seats"] = "integer", ["created_at"] = "timestamp with time zone"
        });
        AssertColumns(hold, new Dictionary<string, string>
        {
            ["id"] = "character varying(40)", ["class_id"] = "character varying(40)",
            ["student_id"] = "character varying(17)", ["status"] = "character varying(20)",
            ["created_at"] = "timestamp with time zone", ["expires_at"] = "timestamp with time zone",
            ["confirmed_at"] = "timestamp with time zone", ["cancelled_at"] = "timestamp with time zone"
        });
        Assert.Equal(new[] { nameof(AdmissionHold.CancelledAt), nameof(AdmissionHold.ConfirmedAt) },
            hold.GetProperties().Where(property => property.IsNullable).Select(property => property.Name).Order());
        Assert.All(admissionClass.GetProperties(), property => Assert.False(property.IsNullable));
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(hold.GetForeignKeys()).DeleteBehavior);

        var activeIndex = Assert.Single(hold.GetIndexes(), index => index.IsUnique);
        Assert.Equal(new[] { "ClassId", "StudentId" }, activeIndex.Properties.Select(property => property.Name));
        Assert.Equal("status IN ('HELD', 'CONFIRMED')", activeIndex.GetFilter());
        Assert.Equal(4, hold.GetIndexes().Count());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(HoldStatus.Held, "HELD")]
    [InlineData(HoldStatus.Confirmed, "CONFIRMED")]
    [InlineData(HoldStatus.Expired, "EXPIRED")]
    [InlineData(HoldStatus.Cancelled, "CANCELLED")]
    public void StatusConversion_UsesExactUppercaseValues(HoldStatus status, string storedValue)
    {
        using var context = CreateContext();
        var converter = context.Model.FindEntityType(typeof(AdmissionHold))!
            .FindProperty(nameof(AdmissionHold.Status))!.GetValueConverter()!;

        Assert.Equal(storedValue, converter.ConvertToProvider(status));
        Assert.Equal(status, converter.ConvertFromProvider(storedValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => converter.ConvertToProvider((HoldStatus)99));
        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("held"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingDatabaseConfiguration_FailsWithoutExposingCredentials(string? connectionString)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));

        Assert.Equal("Database configuration is missing. Set ConnectionStrings__DefaultConnection.", exception.Message);
    }

    private static void AssertColumns(IEntityType entity, Dictionary<string, string> expected)
    {
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var actual = entity.GetProperties().ToDictionary(property => property.GetColumnName(table)!,
            property => property.GetColumnType());
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var (column, type) in expected)
        {
            Assert.Equal(type, actual[column]);
        }
    }
}
