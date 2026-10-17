using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Api.HostedServices;

/// <summary>
/// Dịch vụ nền (IHostedService) chạy bên trong tiến trình API Host.
/// Đảm bảo các tác vụ đưa vào hàng đợi qua API/Status UI được tự động thăm dò, nhận quyền (Lease),
/// và xử lý hoàn tất ngay trong cùng tiến trình khi triển khai đơn lẻ hoặc trong môi trường phát triển (Dev).
/// </summary>
public sealed class JobWorkerHostedService : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly IJobRegistry _jobRegistry;
    private readonly ILogger<JobWorkerHostedService> _logger;
    private readonly string _workerId;

    /// <summary>
    /// Khởi tạo dịch vụ nền với kho lưu trữ tác vụ, danh mục handler và logger.
    /// </summary>
    /// <param name="jobStore">Kho lưu trữ tác vụ nền.</param>
    /// <param name="jobRegistry">Bộ đăng ký handler.</param>
    /// <param name="logger">Logger giám sát.</param>
    public JobWorkerHostedService(
        IJobStore jobStore,
        IJobRegistry jobRegistry,
        ILogger<JobWorkerHostedService> logger)
    {
        _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
        _jobRegistry = jobRegistry ?? throw new ArgumentNullException(nameof(jobRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workerId = $"api-worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
    }

    /// <summary>
    /// Vòng lặp thực thi chính của Worker tích hợp trong API.
    /// </summary>
    /// <param name="stoppingToken">Token dừng host.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("In-process JobWorkerHostedService started. WorkerId: {WorkerId}", _workerId);

        var pollingInterval = TimeSpan.FromSeconds(1);
        var maxPollingInterval = TimeSpan.FromSeconds(5);
        var leaseDuration = TimeSpan.FromMinutes(2);
        var recoveryInterval = TimeSpan.FromSeconds(30);

        var currentDelay = pollingInterval;
        var lastRecoveryCheck = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Quét thu hồi Lease quá hạn định kỳ
                if (DateTimeOffset.UtcNow - lastRecoveryCheck >= recoveryInterval)
                {
                    var recovered = await _jobStore.RecoverExpiredLeasesAsync(stoppingToken);
                    if (recovered > 0)
                    {
                        _logger.LogWarning("API Worker recovered {RecoveredCount} abandoned jobs.", recovered);
                    }
                    lastRecoveryCheck = DateTimeOffset.UtcNow;
                }

                // 2. Thăm dò và chiếm quyền xử lý tác vụ tiếp theo
                var claim = await _jobStore.ClaimNextJobAsync(_workerId, leaseDuration, stoppingToken);

                if (claim is null)
                {
                    await Task.Delay(currentDelay, stoppingToken);
                    currentDelay = TimeSpan.FromTicks(Math.Min(currentDelay.Ticks * 2, maxPollingInterval.Ticks));
                    continue;
                }

                currentDelay = pollingInterval;
                _logger.LogInformation("API Worker claimed job {JobId} (Attempt {Attempt}) of type '{JobType}'. CorrelationId: {CorrelationId}",
                    claim.Job.Id, claim.Attempt.AttemptNumber, claim.Job.Type, claim.Job.CorrelationId);

                await ProcessJobAsync(claim, leaseDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in API JobWorkerHostedService loop.");
                await Task.Delay(currentDelay, stoppingToken);
            }
        }

        _logger.LogInformation("In-process JobWorkerHostedService stopped.");
    }

    /// <summary>
    /// Xử lý một tác vụ đã claim thành công trong Worker tích hợp.
    /// </summary>
    /// <param name="claim">Kết quả claim tác vụ.</param>
    /// <param name="leaseDuration">Thời hạn hiệu lực của lease.</param>
    /// <param name="hostStoppingToken">Token báo hiệu dừng host API.</param>
    private async Task ProcessJobAsync(JobClaimResult claim, TimeSpan leaseDuration, CancellationToken hostStoppingToken)
    {
        var job = claim.Job;
        var leaseToken = claim.LeaseToken;

        var handler = _jobRegistry.GetHandler(job.Type);
        if (handler is null)
        {
            _logger.LogError("No registered handler found for JobType '{JobType}' (JobId: {JobId}, CorrelationId: {CorrelationId}). Failing job permanently.", job.Type, job.Id, job.CorrelationId);
            await _jobStore.FailJobAsync(
                job.Id,
                leaseToken,
                "UNKNOWN_JOB_TYPE",
                $"No handler registered in system for job type '{job.Type}'.",
                isTransient: false,
                hostStoppingToken);
            return;
        }

        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(hostStoppingToken);
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeatTask = RunHeartbeatLoopAsync(job.Id, job.CorrelationId, leaseToken, leaseDuration, jobCts, heartbeatCts.Token);

        var jobRequest = JobRequest.Create(
            job.Type,
            job.PayloadReference,
            job.CreatedBy,
            job.Scope,
            job.IdempotencyKey,
            job.CorrelationId);

        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(job.Id),
            Request = jobRequest,
            AttemptNumber = claim.Attempt.AttemptNumber,
            ReportProgressAsync = async percentage =>
            {
                var currentJob = await _jobStore.GetByIdAsync(job.Id, hostStoppingToken);
                if (currentJob?.CancellationRequestedAt != null || currentJob?.Status == JobStatus.Cancelled)
                {
                    jobCts.Cancel();
                    throw new OperationCanceledException(jobCts.Token);
                }

                await _jobStore.UpdateProgressAsync(job.Id, leaseToken, percentage, hostStoppingToken);
            }
        };

        try
        {
            var result = await handler.ExecuteAsync(context, jobCts.Token);
            await StopHeartbeatAsync(heartbeatCts, heartbeatTask);

            if (result.Succeeded)
            {
                using var completionCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var completed = await _jobStore.CompleteJobAsync(job.Id, leaseToken, result.ResultId, completionCts.Token);
                if (completed)
                {
                    _logger.LogInformation("Job {JobId} completed successfully. CorrelationId: {CorrelationId}, ResultId: {ResultId}", job.Id, job.CorrelationId, result.ResultId);
                }
                else
                {
                    _logger.LogWarning("Failed to commit job completion due to fencing conflict. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
                }
            }
            else
            {
                _logger.LogWarning("Job {JobId} failed. CorrelationId: {CorrelationId}, ErrorCode: {ErrorCode}", job.Id, job.CorrelationId, result.ErrorCode ?? "JOB_FAILED");
                await _jobStore.FailJobAsync(
                    job.Id,
                    leaseToken,
                    result.ErrorCode ?? "JOB_FAILED",
                    result.SafeErrorMessage ?? "The background job encountered an unhandled error.",
                    result.IsTransient,
                    hostStoppingToken);
            }
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            await StopHeartbeatAsync(heartbeatCts, heartbeatTask);
            _logger.LogInformation("Job {JobId} was cancelled. CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
        }
        catch (Exception ex)
        {
            await StopHeartbeatAsync(heartbeatCts, heartbeatTask);
            _logger.LogError(ex, "Unhandled exception processing job {JobId}. CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);

            await _jobStore.FailJobAsync(
                job.Id,
                leaseToken,
                "UNHANDLED_EXCEPTION",
                "An unexpected system exception occurred during job processing.",
                isTransient: true,
                hostStoppingToken);
        }
    }

    /// <summary>
    /// Vòng lặp gia hạn Heartbeat ngầm trong quá trình API Worker xử lý tác vụ.
    /// </summary>
    /// <param name="jobId">Mã định danh tác vụ.</param>
    /// <param name="correlationId">Mã tương quan phục vụ truy vết phân tán.</param>
    /// <param name="leaseToken">Token lease dùng để xác thực fencing.</param>
    /// <param name="leaseDuration">Khoảng thời gian gia hạn lease mỗi lần tick.</param>
    /// <param name="jobCts">Token nguồn dùng để hủy xử lý khi mất lease.</param>
    /// <param name="heartbeatStopToken">Token báo hiệu kết thúc vòng lặp heartbeat.</param>
    private async Task RunHeartbeatLoopAsync(
        Guid jobId,
        string correlationId,
        Guid leaseToken,
        TimeSpan leaseDuration,
        CancellationTokenSource jobCts,
        CancellationToken heartbeatStopToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        try
        {
            while (!heartbeatStopToken.IsCancellationRequested && await timer.WaitForNextTickAsync(heartbeatStopToken))
            {
                var renewed = await _jobStore.HeartbeatAsync(jobId, leaseToken, leaseDuration, heartbeatStopToken);
                if (!renewed)
                {
                    _logger.LogWarning("Heartbeat renewal failed or was fenced in API worker. Triggering job cancellation. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
                    jobCts.Cancel();
                    break;
                }

                _logger.LogDebug("Heartbeat extended successfully in API worker. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in API worker heartbeat loop for JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
            jobCts.Cancel();
        }
    }

    /// <summary>
    /// Hủy và đợi hoàn tất tác vụ heartbeat loop ngầm.
    /// </summary>
    /// <param name="heartbeatCts">CancellationTokenSource điều khiển heartbeat.</param>
    /// <param name="heartbeatTask">Task chạy vòng lặp heartbeat.</param>
    private static async Task StopHeartbeatAsync(CancellationTokenSource heartbeatCts, Task heartbeatTask)
    {
        try
        {
            heartbeatCts.Cancel();
            await heartbeatTask;
        }
        catch
        {
        }
    }
}
