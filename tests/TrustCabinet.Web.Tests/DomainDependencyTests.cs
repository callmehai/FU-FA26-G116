using System.Xml.Linq;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// SCRUM-144: the domain stays free of outside libraries. Anything that needs one (PdfPig, EF Core) sits behind an
/// interface declared in Domain and implemented in Infrastructure, as <c>IPdfInspector</c> is.
/// </summary>
public sealed class DomainDependencyTests
{
    [Fact]
    public void The_domain_project_has_no_package_reference()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "backend", "TrustCabinet.Domain", "TrustCabinet.Domain.csproj"));
        Assert.Empty(project.Descendants().Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (string?)e.Attribute("Include")));
    }

    [Fact]
    public void The_domain_assembly_references_only_the_framework()
    {
        // Judged by where the assembly loads from, not its name: a NuGet package may be called System.* too.
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var outside = typeof(Meeting).Assembly.GetReferencedAssemblies()
            .Where(a => Path.GetDirectoryName(System.Reflection.Assembly.Load(a).Location) != runtime)
            .Select(a => a.Name);
        Assert.Empty(outside);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrustCabinet.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("TrustCabinet.sln not found above the test output.");
    }
}
