using System.Reflection;

namespace SchoolAdmission.UnitTests;

public sealed class DomainArchitectureTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceOuterLayersOrPersistenceFrameworks()
    {
        var domainAssembly = Assembly.Load("SchoolAdmission.Domain");
        string[] forbiddenPrefixes =
        [
            "SchoolAdmission.",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "System.Net.Http"
        ];

        Assert.DoesNotContain(domainAssembly.GetReferencedAssemblies(), reference =>
            forbiddenPrefixes.Any(prefix =>
                reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }
}
