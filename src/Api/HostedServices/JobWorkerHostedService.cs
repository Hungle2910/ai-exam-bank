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
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

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
        _workerId = $"api-worker-{Environment.MachineName[..Math.Min(Environment.MachineName.Length, 12)]}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Vòng lặp thực thi chính của Worker tích hợp trong API.
    /// </summary>
    /// <param name="stoppingToken">Token dừng host.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("In-process JobWorkerHostedService started. WorkerId: {WorkerId}", _workerId);

        var pollingInterval = PollingInterval;
        var maxPollingInterval = MaxPollingInterval;
        var leaseDuration = DefaultLeaseDuration;
        var recoveryInterval = RecoveryInterval;

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
        using var backgroundCts = new CancellationTokenSource();
        var heartbeatTask = RunHeartbeatLoopAsync(job.Id, job.CorrelationId, leaseToken, leaseDuration, jobCts, backgroundCts.Token);
        var cancellationWatcherTask = RunCancellationWatcherAsync(job.Id, job.CorrelationId, jobCts, backgroundCts.Token);

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

            if (result.Succeeded)
            {
                bool completed = false;
                try
                {
                    using var completionCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    completed = await _jobStore.CompleteJobAsync(job.Id, leaseToken, result.ResultId, completionCts.Token);
                }
                catch (Exception commitEx)
                {
                    // Lỗi ghi nhận hoàn tất vào store không được xem là lỗi của handler,
                    // tránh gọi FailJobAsync gây chạy lại side effect đã thực thi thành công.
                    _logger.LogError(commitEx, "Failed to persist job completion to store. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
                    return;
                }

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
                using var failureCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _jobStore.FailJobAsync(
                    job.Id,
                    leaseToken,
                    result.ErrorCode ?? "JOB_FAILED",
                    result.SafeErrorMessage ?? "The background job encountered an unhandled error.",
                    result.IsTransient,
                    failureCts.Token);
            }
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            _logger.LogInformation("Job {JobId} was cancelled. CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing job {JobId}. CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);

            using var errorCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _jobStore.FailJobAsync(
                job.Id,
                leaseToken,
                "UNHANDLED_EXCEPTION",
                "An unexpected system exception occurred during job processing.",
                isTransient: true,
                errorCts.Token);
        }
        finally
        {
            await StopBackgroundTasksAsync(backgroundCts, heartbeatTask, cancellationWatcherTask);
        }
    }

    /// <summary>
    /// Vòng lặp gia hạn Heartbeat ngầm trong quá trình API Worker xử lý tác vụ.
    /// Giữ hiệu lực lease xuyên suốt quá trình thực thi và ghi nhận trạng thái cuối cùng.
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
        using var timer = new PeriodicTimer(HeartbeatInterval);
        var lastSuccessfulRenewal = DateTimeOffset.UtcNow;

        while (!heartbeatStopToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(heartbeatStopToken))
                {
                    break;
                }

                bool renewed;
                try
                {
                    renewed = await _jobStore.HeartbeatAsync(jobId, leaseToken, leaseDuration, heartbeatStopToken);
                }
                catch (OperationCanceledException) when (heartbeatStopToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Transient error renewing heartbeat in API worker for JobId: {JobId}, CorrelationId: {CorrelationId}. Retrying at next tick.", jobId, correlationId);

                    var timeSinceLastRenewal = DateTimeOffset.UtcNow - lastSuccessfulRenewal;
                    if (timeSinceLastRenewal >= leaseDuration - HeartbeatInterval)
                    {
                        _logger.LogError("Lease near expiration without successful renewal ({ElapsedSeconds}s elapsed). Cancelling JobId: {JobId}", timeSinceLastRenewal.TotalSeconds, jobId);
                        jobCts.Cancel();
                        break;
                    }

                    continue;
                }

                if (!renewed)
                {
                    var currentJob = await _jobStore.GetByIdAsync(jobId, heartbeatStopToken);
                    if (currentJob is not null && currentJob.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                    {
                        break;
                    }

                    _logger.LogWarning("Heartbeat renewal failed or was fenced in API worker. Triggering job cancellation. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
                    jobCts.Cancel();
                    break;
                }

                lastSuccessfulRenewal = DateTimeOffset.UtcNow;
                _logger.LogDebug("Heartbeat extended successfully in API worker. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
            }
            catch (OperationCanceledException) when (heartbeatStopToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in API worker heartbeat loop for JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
                jobCts.Cancel();
                break;
            }
        }
    }

    /// <summary>
    /// Vòng lặp giám sát tín hiệu hủy ngầm độc lập với Heartbeat và ReportProgressAsync.
    /// Giúp phát hiện yêu cầu hủy ngay lập tức (mỗi 1 giây) và truyền tín hiệu hủy cho Handler.
    /// Khả năng chịu lỗi: Bỏ qua lỗi đọc tạm thời và tiếp tục thử lại ở tick kế tiếp.
    /// </summary>
    private async Task RunCancellationWatcherAsync(
        Guid jobId,
        string correlationId,
        CancellationTokenSource jobCts,
        CancellationToken watcherStopToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!watcherStopToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(watcherStopToken))
                {
                    break;
                }

                var currentJob = await _jobStore.GetByIdAsync(jobId, watcherStopToken);
                if (currentJob?.CancellationRequestedAt != null || currentJob?.Status == JobStatus.Cancelled)
                {
                    _logger.LogInformation("Cancellation detected by watcher in API worker. Cancelling job execution. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
                    jobCts.Cancel();
                    break;
                }
            }
            catch (OperationCanceledException) when (watcherStopToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transient error while checking cancellation in watcher loop for JobId: {JobId}. Watcher will retry.", jobId);
            }
        }
    }

    /// <summary>
    /// Hủy và đợi hoàn tất các tác vụ nền (heartbeat, cancellation watcher).
    /// </summary>
    private static async Task StopBackgroundTasksAsync(CancellationTokenSource backgroundCts, Task heartbeatTask, Task cancellationWatcherTask)
    {
        try
        {
            backgroundCts.Cancel();
            await Task.WhenAll(heartbeatTask, cancellationWatcherTask);
        }
        catch
        {
        }
    }
}
