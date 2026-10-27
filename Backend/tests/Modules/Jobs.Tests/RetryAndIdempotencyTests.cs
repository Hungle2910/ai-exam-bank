using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AiExamBank.Modules.Jobs.Tests;

[TestClass]
public sealed class RetryAndIdempotencyTests
{
    private static readonly JobScope SchoolScope = JobScope.Create(JobScopeKind.School, "school-hn-01");
    private static readonly JobScope OtherScope = JobScope.Create(JobScopeKind.School, "school-hcm-99");
    private static readonly JobScope MinistryScope = JobScope.Create(JobScopeKind.MinistryExam, "ministry-2026");

    #region 1. Error Taxonomy & Classification Tests

    [TestMethod]
    [DataRow("BEDROCK_TIMEOUT", ErrorCategory.Transient, true)]
    [DataRow("BEDROCK_THROTTLED", ErrorCategory.Transient, true)]
    [DataRow("PROVIDER_TIMEOUT", ErrorCategory.Transient, true)]
    [DataRow("NETWORK_UNAVAILABLE", ErrorCategory.Transient, true)]
    [DataRow("CONCURRENCY_CONFLICT", ErrorCategory.Transient, true)]
    [DataRow("HTTP_429", ErrorCategory.Transient, true)]
    [DataRow("HTTP_503", ErrorCategory.Transient, true)]
    [DataRow("LEASE_EXPIRED", ErrorCategory.Transient, true)]
    [DataRow("UNHANDLED_EXCEPTION", ErrorCategory.Transient, true)]
    [DataRow("CUSTOM_SERVICE_TIMEOUT", ErrorCategory.Transient, true)]
    [DataRow("API_THROTTLED", ErrorCategory.Transient, true)]
    [DataRow("SERVER_UNAVAILABLE", ErrorCategory.Transient, true)]
    [DataRow("ROW_VALIDATION_ERROR", ErrorCategory.Permanent, false)]
    [DataRow("INVALID_CSV_SCHEMA", ErrorCategory.Permanent, false)]
    [DataRow("PAYLOAD_CORRUPTED", ErrorCategory.Permanent, false)]
    [DataRow("BLUEPRINT_INSUFFICIENT_QUESTIONS", ErrorCategory.Permanent, false)]
    [DataRow("QUESTION_NOT_APPROVED", ErrorCategory.Permanent, false)]
    [DataRow("SCOPED_ACCESS_DENIED", ErrorCategory.Permanent, false)]
    [DataRow("IDEMPOTENCY_FINGERPRINT_MISMATCH", ErrorCategory.Permanent, false)]
    [DataRow("RETRY_BUDGET_EXHAUSTED", ErrorCategory.Terminal, false)]
    [DataRow("JOB_CANCELLED", ErrorCategory.Terminal, false)]
    [DataRow("JOB_ABORTED", ErrorCategory.Terminal, false)]
    public void ErrorClassifier_classifies_error_codes_accurately(string errorCode, ErrorCategory expectedCategory, bool expectedRetryable)
    {
        var category = ErrorClassifier.Classify(errorCode);
        var isRetryable = ErrorClassifier.IsRetryable(errorCode);

        Assert.AreEqual(expectedCategory, category, $"Error '{errorCode}' classification mismatch.");
        Assert.AreEqual(expectedRetryable, isRetryable, $"Error '{errorCode}' retryable flag mismatch.");
    }

    [TestMethod]
    public void ErrorClassifier_handles_null_empty_and_unknown_gracefully()
    {
        Assert.AreEqual(ErrorCategory.Permanent, ErrorClassifier.Classify(null));
        Assert.AreEqual(ErrorCategory.Permanent, ErrorClassifier.Classify(""));
        Assert.AreEqual(ErrorCategory.Permanent, ErrorClassifier.Classify("   "));
        Assert.IsFalse(ErrorClassifier.IsRetryable(null));
        Assert.IsFalse(ErrorClassifier.IsRetryable("SOME_RANDOM_BUSINESS_ERROR"));
    }

    #endregion

    #region 2. Backoff & Jitter Calculator Tests

    [TestMethod]
    public void BackoffJitterCalculator_deterministic_exponential_calculation()
    {
        // Khi ghim jitterFactor = 1.0 (không có jitter)
        var delay1 = BackoffJitterCalculator.CalculateDelay(1, jitterFactor: 1.0);
        var delay2 = BackoffJitterCalculator.CalculateDelay(2, jitterFactor: 1.0);
        var delay3 = BackoffJitterCalculator.CalculateDelay(3, jitterFactor: 1.0);

        Assert.AreEqual(TimeSpan.FromSeconds(30), delay1, "Lần 1 không jitter phải là 30s.");
        Assert.AreEqual(TimeSpan.FromSeconds(60), delay2, "Lần 2 không jitter phải là 60s (30 * 2^1).");
        Assert.AreEqual(TimeSpan.FromSeconds(120), delay3, "Lần 3 không jitter phải là 120s (30 * 2^2).");
    }

    [TestMethod]
    public void BackoffJitterCalculator_applies_jitter_within_twenty_percent_bounds()
    {
        for (var i = 0; i < 50; i++)
        {
            var delay1 = BackoffJitterCalculator.CalculateDelay(1);
            Assert.IsTrue(delay1 >= TimeSpan.FromSeconds(24) && delay1 <= TimeSpan.FromSeconds(36),
                $"Attempt 1 delay {delay1.TotalSeconds}s out of [24s, 36s] range.");

            var delay2 = BackoffJitterCalculator.CalculateDelay(2);
            Assert.IsTrue(delay2 >= TimeSpan.FromSeconds(48) && delay2 <= TimeSpan.FromSeconds(72),
                $"Attempt 2 delay {delay2.TotalSeconds}s out of [48s, 72s] range.");
        }
    }

    [TestMethod]
    public void BackoffJitterCalculator_clamps_to_max_delay()
    {
        var hugeAttemptDelay = BackoffJitterCalculator.CalculateDelay(20, jitterFactor: 1.0);
        Assert.AreEqual(BackoffJitterCalculator.DefaultMaxDelay, hugeAttemptDelay, "Delay phải bị chặn trần bởi MaxDelay (15 phút).");
    }

    [TestMethod]
    public void BackoffJitterCalculator_CalculateAvailableAt_advances_utc_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var availableAt = BackoffJitterCalculator.CalculateAvailableAt(1, fromUtc: now, jitterFactor: 1.0);
        Assert.AreEqual(now.AddSeconds(30), availableAt);
    }

    #endregion

    #region 3. Store Retry & Attempts Tracking Tests

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Store_tracks_execution_duration_and_attempt_history(bool useFileStore)
    {
        var now = DateTimeOffset.UtcNow;
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobs-attempts-{Guid.NewGuid():N}.json");
        using var fileStore = useFileStore ? new FileJobStore(tempFile, () => now) : null;
        IJobStore store = fileStore is not null ? fileStore : new InMemoryJobStore(() => now);

        try
        {
            var req = JobRequest.Create("ExamGen", "s3://payload", "user-1", SchoolScope, "idem-att", "corr-att");
            var job = await store.EnqueueAsync(req, "fp-att");

            // Lượt 1: Claim và Fail sau 2.5 giây mô phỏng
            var claim1 = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim1);

            now = now.AddMilliseconds(2500);
            await store.FailJobAsync(job.Id, claim1.LeaseToken, "BEDROCK_TIMEOUT", "Bedrock timed out.", isTransient: true);

            var attemptsAfterFail = await store.GetAttemptsAsync(job.Id);
            Assert.HasCount(1, attemptsAfterFail);
            Assert.AreEqual(AttemptStatus.Failed, attemptsAfterFail[0].Status);
            Assert.AreEqual("worker-01", attemptsAfterFail[0].WorkerId);
            Assert.AreEqual("BEDROCK_TIMEOUT", attemptsAfterFail[0].ErrorCode);
            Assert.AreEqual(2500, attemptsAfterFail[0].ExecutionDurationMs);

            // Tua qua backoff cooldown để lượt 2 chạy
            now = now.AddMinutes(2);
            var claim2 = await store.ClaimNextJobAsync("worker-02", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim2);

            now = now.AddMilliseconds(1200);
            await store.CompleteJobAsync(job.Id, claim2.LeaseToken, "exam-result-99");

            var attemptsAfterSuccess = await store.GetAttemptsAsync(job.Id);
            Assert.HasCount(2, attemptsAfterSuccess);
            Assert.AreEqual(AttemptStatus.Completed, attemptsAfterSuccess[1].Status);
            Assert.AreEqual("worker-02", attemptsAfterSuccess[1].WorkerId);
            Assert.AreEqual(1200, attemptsAfterSuccess[1].ExecutionDurationMs);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Store_manual_retry_resets_failed_job_to_pending(bool useFileStore)
    {
        var now = DateTimeOffset.UtcNow;
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobs-manual-retry-{Guid.NewGuid():N}.json");
        using var fileStore = useFileStore ? new FileJobStore(tempFile, () => now) : null;
        IJobStore store = fileStore is not null ? fileStore : new InMemoryJobStore(() => now);

        try
        {
            var req = JobRequest.Create("CsvImport", "s3://csv", "teacher-1", SchoolScope, "idem-retry-manual", "corr-retry-manual");
            var job = await store.EnqueueAsync(req, "fp-retry-manual");

            // Giả lập worker claim và fail vĩnh viễn (exhausted hoặc permanent)
            var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim);
            await store.FailJobAsync(job.Id, claim.LeaseToken, "ROW_VALIDATION_ERROR", "Data invalid", isTransient: false);

            var failedJob = await store.GetByIdAsync(job.Id);
            Assert.IsNotNull(failedJob);
            Assert.AreEqual(JobStatus.Failed, failedJob.Status);

            // Thực hiện Retry thủ công
            now = now.AddMinutes(1);
            var retriedJob = await store.RetryJobAsync(job.Id, "teacher-1", "Sửa lỗi dữ liệu CSV nguồn");

            Assert.AreEqual(JobStatus.Pending, retriedJob.Status);
            Assert.AreEqual(0, retriedJob.AttemptCount, "AttemptCount phải được reset về 0 để mở ra chu kỳ thử lại mới.");
            Assert.IsNull(retriedJob.ErrorCode);
            Assert.IsNull(retriedJob.SafeErrorMessage);
            Assert.AreEqual("Sửa lỗi dữ liệu CSV nguồn", retriedJob.RetryReason);
            Assert.AreEqual("teacher-1", retriedJob.LastRetryRequestedBy);
            Assert.AreEqual(now, retriedJob.LastRetryRequestedAt);

            // Worker claim lại sau manual retry
            var retryClaim = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(retryClaim);
            Assert.IsTrue(retryClaim.Attempt.IsManualRetry, "Lượt claim mới phải nhận diện IsManualRetry = true.");
            Assert.AreEqual("Sửa lỗi dữ liệu CSV nguồn", retryClaim.Attempt.RetryReason);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task Store_manual_retry_throws_for_invalid_states()
    {
        var store = new InMemoryJobStore();
        var req = JobRequest.Create("ExamGen", "s3://ref", "user-1", SchoolScope, "idem-inv", "corr-inv");
        var job = await store.EnqueueAsync(req, "fp-inv");

        // 1. Không thể retry job đang Pending
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await store.RetryJobAsync(job.Id, "user-1", "reason"));

        // 2. Không thể retry job đang Running
        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await store.RetryJobAsync(job.Id, "user-1", "reason"));

        // 3. Không thể retry job đã Completed
        await store.CompleteJobAsync(job.Id, claim.LeaseToken, "res-ok");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await store.RetryJobAsync(job.Id, "user-1", "reason"));
    }

    #endregion

    #region 4. Scoped Authorization for Retry & Attempts Endpoints

    [TestMethod]
    public async Task JobService_enforces_scoped_authorization_on_retry_and_attempts()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var req = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-scope-auth", "corr-scope-auth");
        var jobRef = await service.EnqueueAsync(req);

        // Chuyển job sang Failed để đủ điều kiện retry (sử dụng RETRY_BUDGET_EXHAUSTED đại diện cho job cạn hạn mức thử lại tự động)
        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);
        await store.FailJobAsync(jobRef.Value, claim.LeaseToken, "RETRY_BUDGET_EXHAUSTED", "Retry budget exhausted", isTransient: false);

        // Case 1: Caller từ SchoolScope khác (school-hcm-99) cố gắng Retry -> Phải bị chặn 403 (UnauthorizedAccessException)
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
            await service.RetryJobAsync(jobRef, "teacher-hcm", OtherScope, "Cross-scope retry attempt"));

        // Case 2: Caller từ MinistryScope cố gắng Retry job của School -> Phải bị chặn 403
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
            await service.RetryJobAsync(jobRef, "ministry-admin", MinistryScope, "Cross-scope retry attempt"));

        // Case 3: Caller từ SchoolScope khác cố gắng xem Attempts History -> Phải bị chặn 403
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
            await service.GetJobAttemptsAsync(jobRef, "teacher-hcm", OtherScope));

        // Case 4: Caller đúng Scope (school-hn-01) -> Thành công
        var retriedStatus = await service.RetryJobAsync(jobRef, "teacher-hn", SchoolScope, "Hợp lệ cùng phạm vi");
        Assert.AreEqual(JobStatus.Pending, retriedStatus.Status);

        var attempts = await service.GetJobAttemptsAsync(jobRef, "teacher-hn", SchoolScope);
        Assert.HasCount(1, attempts);
    }

    #endregion

    #region 5. Idempotency Result Reuse Tests

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Enqueue_completed_job_with_same_idempotency_key_reuses_result_without_reexecution(bool useFileStore)
    {
        var now = DateTimeOffset.UtcNow;
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobs-idempotency-{Guid.NewGuid():N}.json");
        using var fileStore = useFileStore ? new FileJobStore(tempFile, () => now) : null;
        IJobStore store = fileStore is not null ? fileStore : new InMemoryJobStore(() => now);

        try
        {
            var req1 = JobRequest.Create("ExamGen", "s3://blueprint-math", "user-1", SchoolScope, "idem-reuse-key", "corr-1");
            var fingerprint1 = JobIdempotency.ComputeRequestFingerprint(req1.JobType, req1.PayloadReference, req1.Scope.Kind, req1.Scope.ResourceId);
            var job1 = await store.EnqueueAsync(req1, fingerprint1);

            // Thực thi và hoàn thành job1
            var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim1);
            await store.CompleteJobAsync(job1.Id, claim1.LeaseToken, "exam-result-cached-42");

            // Enqueue lần 2 với cùng IdempotencyKey và PayloadReference
            var req2 = JobRequest.Create("ExamGen", "s3://blueprint-math", "user-1", SchoolScope, "idem-reuse-key", "corr-2");
            var fingerprint2 = JobIdempotency.ComputeRequestFingerprint(req2.JobType, req2.PayloadReference, req2.Scope.Kind, req2.Scope.ResourceId);
            var job2 = await store.EnqueueAsync(req2, fingerprint2);

            // Job2 phải tái sử dụng lại chính xác Job1 đã hoàn thành và trả về kết quả ngay lập tức
            Assert.AreEqual(job1.Id, job2.Id, "Idempotency phải trả về cùng ID tác vụ.");
            Assert.AreEqual(JobStatus.Completed, job2.Status);
            Assert.AreEqual("exam-result-cached-42", job2.ResultId, "ResultId phải được tái sử dụng nguyên vẹn.");

            // Không có job nào được đưa vào hàng đợi chờ xử lý
            var claim2 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNull(claim2, "Không được có job nào thừa trong hàng đợi thực thi.");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region 6. Adapter Timeout-After-Write Recovery Tests

    [TestMethod]
    public async Task ExamGenerationAdapter_recovers_gracefully_from_timeout_after_write()
    {
        var handler = new ExamGenerationJobHandler();
        var cancellationSource = new CancellationTokenSource();

        var request = JobRequest.Create("ExamGeneration", "simulate-timeout-after-write", "user-1", SchoolScope, "idem-exam-recovery", "corr-exam-recovery");
        var context1 = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        // Lần 1: Giả lập timeout sau khi đã ghi kết quả
        var result1 = await handler.ExecuteAsync(context1, cancellationSource.Token);
        Assert.IsTrue(result1.Succeeded, "Lần đầu ExamGenerationJobHandler sinh snapshot thành công.");
        Assert.IsNotNull(result1.ResultId);

        // Lần 2 (Thử lại / Retry attempt 2): Handler kiểm tra trước khi ghi và phát hiện kết quả đã tồn tại
        var context2 = new JobExecutionContext
        {
            JobId = context1.JobId,
            Request = request,
            AttemptNumber = 2
        };

        var result2 = await handler.ExecuteAsync(context2, cancellationSource.Token);
        Assert.IsTrue(result2.Succeeded, "Thử lại sau timeout-after-write phải thành công qua cơ chế check-before-write.");
        Assert.AreEqual(result1.ResultId, result2.ResultId, "Phải tái sử dụng cùng snapshot Id mà không sinh đề thứ hai.");
    }

    [TestMethod]
    public async Task CsvImportAdapter_recovers_gracefully_from_timeout_after_write()
    {
        var handler = new CsvImportJobHandler();
        var cancellationSource = new CancellationTokenSource();

        var request = JobRequest.Create("CsvImport", "simulate-timeout-after-write", "teacher-1", SchoolScope, "idem-csv-recovery", "corr-csv-recovery");
        var context1 = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        // Lần 1: Lần đầu CsvImport sinh batch thành công
        var result1 = await handler.ExecuteAsync(context1, cancellationSource.Token);
        Assert.IsTrue(result1.Succeeded);
        Assert.IsNotNull(result1.ResultId);

        // Lần 2: Check-before-write phát hiện batch đã ghi thành công
        var context2 = new JobExecutionContext
        {
            JobId = context1.JobId,
            Request = request,
            AttemptNumber = 2
        };

        var result2 = await handler.ExecuteAsync(context2, cancellationSource.Token);
        Assert.IsTrue(result2.Succeeded, "Thử lại sau timeout-after-write phải thành công phục hồi mà không tạo trùng lặp.");
        Assert.AreEqual(result1.ResultId, result2.ResultId, "Phải tái sử dụng cùng Import Batch Id.");
    }

    #endregion
}
