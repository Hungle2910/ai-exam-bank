using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Persistence.Exams;

namespace AiExamBank.Api.Exams;

public sealed class ApprovedExamSelectionHandler(IServiceScopeFactory scopes) : IJobHandler
{
    public string JobType => "ApprovedExamSelection";

    public async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        if (!Guid.TryParse(context.Request.PayloadReference, out var examId) ||
            !Guid.TryParse(context.Request.RequesterId, out var actor) ||
            context.Request.Scope.Kind != JobScopeKind.School ||
            !Guid.TryParse(context.Request.Scope.ResourceId, out var school))
            return JobExecutionResult.Failure("EXAM_REQUEST_INVALID", "Invalid exam selection request.");
        await using var scope = scopes.CreateAsyncScope();
        var done = await scope.ServiceProvider.GetRequiredService<ExamGenerationStore>().ExecuteAsync(examId, actor, school, ct);
        return done ? JobExecutionResult.Success(examId.ToString("D"))
            : JobExecutionResult.Failure("EXAM_SCOPE_OR_BLUEPRINT_CHANGED", "Quyền truy cập hoặc ma trận đã thay đổi. Hãy tạo yêu cầu mới.");
    }
}
