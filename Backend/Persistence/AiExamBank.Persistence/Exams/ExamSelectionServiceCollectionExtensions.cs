using AiExamBank.Modules.Exams.Application;
using AiExamBank.Modules.Jobs.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AiExamBank.Persistence.Exams;

public static class ExamSelectionServiceCollectionExtensions
{
    public static IServiceCollection AddExamSelection(this IServiceCollection services)
    {
        services.AddScoped<BlueprintStore>();
        services.AddScoped<ExamSelectionService>();
        services.AddScoped<ExamGenerationStore>();
        services.AddSingleton<IJobHandler, ApprovedExamSelectionHandler>();
        return services;
    }
}
