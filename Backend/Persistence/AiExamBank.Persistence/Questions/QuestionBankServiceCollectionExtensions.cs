using AiExamBank.Modules.Questions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiExamBank.Persistence.Questions;

public static class QuestionBankServiceCollectionExtensions
{
    /// <summary>
    /// Registers the question bank services. They share the request's <see cref="AiExamBankDbContext"/>, so they
    /// join whatever transaction the caller has open.
    /// </summary>
    public static IServiceCollection AddQuestionBank(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IQuestionWriter, QuestionWriter>();
        services.AddScoped<IQuestionCatalog, QuestionCatalog>();
        services.AddScoped<IQuestionQueries, QuestionQueries>();
        services.AddScoped<IApprovedQuestionReader, ApprovedQuestionReader>();
        return services;
    }
}
