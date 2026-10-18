using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Worker;

/// <summary>
/// Dịch vụ nền (BackgroundService) chịu trách nhiệm thăm dò hàng đợi (Worker Polling),
/// chiếm quyền thực thi có thời hạn (Atomic Lease Claim), duy trì nhịp tim định kỳ (Heartbeat 30s),
/// lắng nghe và xử lý dừng tác vụ khi nhận CancellationToken hoặc yêu cầu hủy,
/// và tự động thu hồi tác vụ bị bỏ hoang khi các worker khác bị crash/mất kết nối (Lease Recovery).
/// </summary>
public sealed class JobWorkerService : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly IJobRegistry _jobRegistry;
    private readonly JobWorkerOptions _options;
    private readonly ILogger<JobWorkerService> _logger;

    /// <summary>
    /// Khởi tạo JobWorkerService với kho lưu trữ tác vụ, danh mục handler, cấu hình và logger.
    /// </summary>
    /// <param name="jobStore">Kho lưu trữ tác vụ nền (IJobStore).</param>
    /// <param name="jobRegistry">Bộ đăng ký handler (IJobRegistry).</param>
    /// <param name="options">Tùy chọn cấu hình Worker.</param>
    /// <param name="logger">Logger giám sát.</param>
    public JobWorkerService(
        IJobStore jobStore,
        IJobRegistry jobRegistry,
        IOptions<JobWorkerOptions> options,
        ILogger<JobWorkerService> logger)
    {
        _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
        _jobRegistry = jobRegistry ?? throw new ArgumentNullException(nameof(jobRegistry));
        _options = options?.Value ?? new JobWorkerOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Vòng lặp thực thi chính của tiến trình Worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorkerService started. WorkerId: {WorkerId}, PollingInterval: {PollingInterval}s",
            _options.WorkerId, _options.PollingInterval.TotalSeconds);

        var currentDelay = _options.PollingInterval;
        var lastRecoveryCheck = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Quét thu hồi các Lease bị bỏ hoang định kỳ
                if (DateTimeOffset.UtcNow - lastRecoveryCheck >= _options.RecoveryInterval)
                {
                    var recovered = await _jobStore.RecoverExpiredLeasesAsync(stoppingToken);
                    if (recovered > 0)
                    {
                        _logger.LogWarning("Worker recovered {RecoveredCount} abandoned jobs with expired leases.", recovered);
                    }
                    lastRecoveryCheck = DateTimeOffset.UtcNow;
                }

                // 2. Thăm dò và chiếm quyền xử lý tác vụ tiếp theo (Atomic Claim)
                var claim = await _jobStore.ClaimNextJobAsync(_options.WorkerId, _options.LeaseDuration, stoppingToken);

                if (claim is null)
                {
                    // Hàng đợi đang trống => Áp dụng Adaptive Exponential Backoff
                    await Task.Delay(currentDelay, stoppingToken);
                    currentDelay = TimeSpan.FromTicks(Math.Min(currentDelay.Ticks * 2, _options.MaxPollingInterval.Ticks));
                    continue;
                }

                // Đã claim thành công job => reset lại chu kỳ polling về mức tối thiểu
                currentDelay = _options.PollingInterval;

                _logger.LogInformation("Job claimed successfully. JobId: {JobId}, Attempt: {Attempt}, Type: {JobType}, CorrelationId: {CorrelationId}",
                    claim.Job.Id, claim.Attempt.AttemptNumber, claim.Job.Type, claim.Job.CorrelationId);

                // 3. Xử lý tác vụ có quản lý Lease, Heartbeat và Cancellation Token
                await ProcessClaimedJobAsync(claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("JobWorkerService received stopping signal. Shutting down gracefully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in JobWorkerService polling loop.");
                await Task.Delay(currentDelay, stoppingToken);
            }
        }

        _logger.LogInformation("JobWorkerService stopped.");
    }

    /// <summary>
    /// Xử lý một tác vụ đã claim thành công: khởi động heartbeat ngầm, truyền CancellationToken,
    /// theo dõi hủy tác vụ và cập nhật trạng thái cuối cùng.
    /// </summary>
    private async Task ProcessClaimedJobAsync(JobClaimResult claim, CancellationToken hostStoppingToken)
    {
        var job = claim.Job;
        var leaseToken = claim.LeaseToken;

        // 1. Kiểm tra xem loại tác vụ đã có Handler đăng ký hay chưa
        var handler = _jobRegistry.GetHandler(job.Type);
        if (handler is null)
        {
            _logger.LogError("No registered handler found for JobType '{JobType}'. Failing job permanently.", job.Type);
            await _jobStore.FailJobAsync(
                job.Id,
                leaseToken,
                "UNKNOWN_JOB_TYPE",
                $"No handler registered in system for job type '{job.Type}'.",
                isTransient: false,
                hostStoppingToken);
            return;
        }

        // 2. Thiết lập CancellationTokenSource liên kết:
        // Sẽ bị trigger nếu (A) Host server tắt máy, hoặc (B) Người dùng/hệ thống yêu cầu hủy job,
        // hoặc (C) Tín hiệu Heartbeat thất bại (Worker bị Fenced/tước quyền).
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(hostStoppingToken);

        // 3. Khởi động Timer/Task duy trì Heartbeat ngầm mỗi 30 giây và Watcher giám sát hủy mỗi 1 giây
        using var backgroundCts = new CancellationTokenSource();
        var heartbeatTask = RunHeartbeatLoopAsync(job.Id, job.CorrelationId, leaseToken, jobCts, backgroundCts.Token);
        var cancellationWatcherTask = RunCancellationWatcherAsync(job.Id, job.CorrelationId, jobCts, backgroundCts.Token);

        // 4. Tạo JobExecutionContext và hàm callback báo cáo tiến độ
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
                // Kiểm tra xem job có bị yêu cầu hủy ở giữa chừng không
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
            // 5. Thực thi nghiệp vụ qua IJobHandler
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
                    _logger.LogInformation("Job completed successfully. JobId: {JobId}, CorrelationId: {CorrelationId}, ResultId: {ResultId}", job.Id, job.CorrelationId, result.ResultId);
                }
                else
                {
                    _logger.LogWarning("Failed to commit job completion due to fencing conflict. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
                }
            }
            else
            {
                _logger.LogWarning("Job handler reported failure. JobId: {JobId}, CorrelationId: {CorrelationId}, ErrorCode: {ErrorCode}, IsTransient: {IsTransient}",
                    job.Id, job.CorrelationId, result.ErrorCode, result.IsTransient);

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
            if (hostStoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Job execution aborted due to Host shutdown. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
                // Để nguyên để lần khởi động sau Worker có thể phục hồi theo Lease
            }
            else
            {
                _logger.LogInformation("Job execution was cancelled. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during job execution. JobId: {JobId}, CorrelationId: {CorrelationId}", job.Id, job.CorrelationId);

            using var errorCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            // Xử lý lỗi ngoại lệ không mong muốn như lỗi tạm thời (Transient)
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
    /// Vòng lặp ngầm gia hạn Heartbeat mỗi 30 giây trong suốt quá trình Worker chạy job.
    /// Giữ hiệu lực lease xuyên suốt quá trình thực thi và ghi nhận trạng thái cuối cùng.
    /// Nếu Heartbeat trả về false (do Job bị hủy hoặc Worker bị tước quyền Lease), lập tức kích hoạt hủy (Cancel).
    /// </summary>
    private async Task RunHeartbeatLoopAsync(
        Guid jobId,
        string correlationId,
        Guid leaseToken,
        CancellationTokenSource jobCts,
        CancellationToken heartbeatStopToken)
    {
        using var timer = new PeriodicTimer(_options.HeartbeatInterval);

        try
        {
            while (!heartbeatStopToken.IsCancellationRequested && await timer.WaitForNextTickAsync(heartbeatStopToken))
            {
                var renewed = await _jobStore.HeartbeatAsync(jobId, leaseToken, _options.LeaseDuration, heartbeatStopToken);

                if (!renewed)
                {
                    var currentJob = await _jobStore.GetByIdAsync(jobId, heartbeatStopToken);
                    if (currentJob is not null && currentJob.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                    {
                        break;
                    }

                    _logger.LogWarning("Heartbeat renewal failed or was fenced. Triggering job cancellation. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
                    jobCts.Cancel();
                    break;
                }

                _logger.LogDebug("Heartbeat extended successfully. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
            }
        }
        catch (OperationCanceledException)
        {
            // Heartbeat loop kết thúc bình thường khi job xong
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in heartbeat loop for JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
            jobCts.Cancel();
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
                    _logger.LogInformation("Cancellation detected by watcher in worker service. Cancelling job execution. JobId: {JobId}, CorrelationId: {CorrelationId}", jobId, correlationId);
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
                _logger.LogWarning(ex, "Transient error checking cancellation in watcher for JobId: {JobId}. Retrying on next tick.", jobId);
            }
        }
    }

    /// <summary>
    /// Hủy và đợi hoàn tất các tác vụ nền (heartbeat, cancellation watcher) của Worker độc lập.
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
