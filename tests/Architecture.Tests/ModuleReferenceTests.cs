using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Import.Contracts;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Knowledge.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Review.Contracts;
using System.Xml.Linq;

namespace AiExamBank.Architecture.Tests;

[TestClass]
public sealed class ModuleReferenceTests
{
    [TestMethod]
    public void Domain_modules_do_not_depend_on_hosts_or_provider_sdks()
    {
        var assemblies = new[]
        {
            typeof(ActorContext).Assembly,
            typeof(QuestionRevisionRef).Assembly,
            typeof(ReviewDecisionRequest).Assembly,
            typeof(BlueprintSlot).Assembly,
            typeof(ImportBatchRef).Assembly,
            typeof(SourceCitation).Assembly,
            typeof(JobRef).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var forbidden = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null &&
                    (name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                     name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                     name.StartsWith("AWSSDK", StringComparison.Ordinal) ||
                     name.StartsWith("AiExamBank.Api", StringComparison.Ordinal) ||
                     name.StartsWith("AiExamBank.Worker", StringComparison.Ordinal)))
                .ToArray();

            Assert.IsEmpty(forbidden, $"{assembly.GetName().Name} depends on {string.Join(", ", forbidden)}");
        }
    }

    [TestMethod]
    public void Module_projects_do_not_declare_host_or_provider_references()
    {
        var root = FindRepositoryRoot();
        var moduleDirectory = Path.Combine(root, "src", "Modules");

        foreach (var project in Directory.EnumerateFiles(moduleDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            var forbidden = FindForbiddenReferences(XDocument.Load(project), project, root);
            Assert.IsEmpty(forbidden, $"{Path.GetFileName(project)} declares {string.Join(", ", forbidden)}");
        }
    }

    [TestMethod]
    public void Unused_forbidden_project_and_package_references_are_detected()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "src", "Modules", "Questions", "AiExamBank.Modules.Questions.csproj");
        var fixture = XDocument.Parse("""
            <Project>
              <ItemGroup>
                <ProjectReference Include="..\..\Api\AiExamBank.Api.csproj" />
                <PackageReference Include="AWSSDK.S3" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var forbidden = FindForbiddenReferences(fixture, project, root);

        Assert.HasCount(2, forbidden);
    }

    private static IReadOnlyList<string> FindForbiddenReferences(XDocument document, string projectPath, string root)
    {
        var forbidden = new List<string>();
        var projectDirectory = Path.GetDirectoryName(projectPath)!;

        foreach (var element in document.Descendants())
        {
            var include = (string?)element.Attribute("Include");
            if (string.IsNullOrWhiteSpace(include)) continue;

            if (element.Name.LocalName == "ProjectReference")
            {
                var normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var target = Path.GetFullPath(Path.Combine(projectDirectory, normalized));
                var relative = Path.GetRelativePath(root, target).Replace('\\', '/');
                if (!relative.StartsWith("src/Modules/", StringComparison.OrdinalIgnoreCase))
                    forbidden.Add($"ProjectReference:{include}");
            }
            else if (element.Name.LocalName == "PackageReference" && IsProviderPackage(include))
            {
                forbidden.Add($"PackageReference:{include}");
            }
        }

        return forbidden;
    }

    private static bool IsProviderPackage(string name) =>
        new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient",
                "Microsoft.Data.Sqlite", "AWSSDK", "Npgsql", "MySqlConnector", "Pomelo.EntityFrameworkCore",
                "Oracle.EntityFrameworkCore" }
            .Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiExamBank.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("AiExamBank.slnx was not found above the test output directory.");
    }
}
