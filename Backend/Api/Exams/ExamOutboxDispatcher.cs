using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Persistence;
using AiExamBank.Persistence.Exams;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api.Exams;

/// <summary>Retries persisted requests after an enqueue crash; the M5 idempotency key is the stable exam ID.</summary>
public sealed class ExamOutboxDispatcher(IServiceScopeFactory scopes, IJobPublisher publisher, ILogger<ExamOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AiExamBankDbContext>();
                var pending = await db.Set<ExamGenerationEntity>().Where(x => x.JobId == null)
                    .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(20)
                    .Select(x => new { x.Id, x.OwnerUserId, x.SchoolId }).ToListAsync(stoppingToken);
                foreach (var exam in pending)
                {
                    var id = exam.Id.ToString("D");
                    var job = await publisher.EnqueueAsync(JobRequest.Create(ApprovedExamSelectionHandler.TypeName, id,
                        exam.OwnerUserId.ToString("D"), JobScope.Create(JobScopeKind.School, exam.SchoolId.ToString("D")), id, id), stoppingToken);
                    // Only update the delivery marker. A worker may already have committed the result.
                    await db.Set<ExamGenerationEntity>().Where(x => x.Id == exam.Id && x.JobId == null)
                        .ExecuteUpdateAsync(update => update.SetProperty(x => x.JobId, job.Value), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Exam request dispatch failed; persisted requests will be retried."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
