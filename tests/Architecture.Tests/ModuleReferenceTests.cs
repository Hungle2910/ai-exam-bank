using AiExamBank.Modules.Exams.Domain;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Import.Contracts;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Knowledge.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Review.Contracts;

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
}
