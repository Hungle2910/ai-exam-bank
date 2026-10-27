using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AiExamBank.Modules.Jobs.Tests;

[TestClass]
public sealed class FailureHandlingAndResilienceTests
{
    private static readonly JobScope SchoolScope = JobScope.Create(JobScopeKind.School, "school-hn-01");

    #region 1. AWS Error Taxonomy & Actionable Mapping Tests

    [TestMethod]
    [DataRow(AwsErrorCodes.BedrockThrottled, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.BedrockModelTimeout, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.S3SlowDown, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.S3Timeout, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.StaleLeaseRecovered, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.CircuitBreakerOpen, ErrorCategory.Transient, true)]
    [DataRow(AwsErrorCodes.BedrockQuotaExceeded, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.BedrockAccessDenied, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.AiMalformedOutput, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.S3AccessDenied, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.S3FileNotFound, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.RagRetrievalEmpty, ErrorCategory.Permanent, false)]
    [DataRow(AwsErrorCodes.KnowledgeBaseNotFound, ErrorCategory.Permanent, false)]
    public void ErrorClassifier_classifies_all_aws_provider_errors_accurately(
        string errorCode,
        ErrorCategory expectedCategory,
        bool expectedRetryable)
    {
        var category = ErrorClassifier.Classify(errorCode);
        var isRetryable = ErrorClassifier.IsRetryable(errorCode);

        Assert.AreEqual(expectedCategory, category, $"ErrorCode '{errorCode}' category mismatch.");
        Assert.AreEqual(expectedRetryable, isRetryable, $"ErrorCode '{errorCode}' retryable flag mismatch.");
    }

    [TestMethod]
    public void AwsErrorMapping_returns_actionable_user_messages_for_all_aws_errors()
    {
        string[] allCodes =
        [
            AwsErrorCodes.BedrockThrottled,
            AwsErrorCodes.BedrockQuotaExceeded,
            AwsErrorCodes.BedrockAccessDenied,
            AwsErrorCodes.BedrockModelTimeout,
            AwsErrorCodes.AiMalformedOutput,
            AwsErrorCodes.S3AccessDenied,
            AwsErrorCodes.S3FileNotFound,
            AwsErrorCodes.S3SlowDown,
            AwsErrorCodes.S3Timeout,
            AwsErrorCodes.RagRetrievalEmpty,
            AwsErrorCodes.KnowledgeBaseNotFound,
            AwsErrorCodes.StaleLeaseRecovered,
            AwsErrorCodes.CircuitBreakerOpen
        ];

        foreach (var code in allCodes)
        {
            var msg = AwsErrorMapping.GetActionableMessage(code);
            Assert.IsFalse(string.IsNullOrWhiteSpace(msg), $"Actionable message for '{code}' must not be empty.");
            Assert.IsFalse(msg.Contains("Exception", StringComparison.OrdinalIgnoreCase), $"Message for '{code}' should be human-friendly.");
        }
    }

    [TestMethod]
    public void AwsErrorMapping_sanitizes_stack_traces_and_secrets_strictly()
    {
        const string leakyMessage = "System.NullReferenceException: Object reference not set at System.Data.SqlClient.Connection.Open() at C:\\Users\\Dev\\Repo\\Secrets.cs:line 45 with AKIAIOSFODNN7EXAMPLE and arn:aws:iam::123456789012:user/Alice";

        var sanitized = AwsErrorMapping.SanitizeForClient(leakyMessage, AwsErrorCodes.BedrockThrottled);

        Assert.IsFalse(sanitized.Contains("System.", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sanitized.Contains("line 45", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sanitized.Contains("AKIA", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.Contains("arn:aws:", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sanitized.Contains("C:\\Users", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(sanitized.Contains("AI đang quá tải", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AwsErrorMapping_sanitizes_temporary_keys_and_linux_paths_and_custom_stacktraces()
    {
        const string leakyMessage = "at AiExamBank.Modules.Jobs.Handler.Execute() in /var/app/code/JobService.cs with ASIAIOSFODNN7EXAMPLE and arn:aws-cn:bedrock:cn-north-1:123456789012:model/claude";

        var sanitized = AwsErrorMapping.SanitizeForClient(leakyMessage, AwsErrorCodes.BedrockAccessDenied);

        Assert.IsFalse(sanitized.Contains("AiExamBank", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sanitized.Contains("/var/app", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sanitized.Contains("ASIA", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.Contains("arn:aws-cn:", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(sanitized.Contains("IAM", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AwsErrorMapping_preserves_benign_asia_timezone_and_sanitizes_extended_unix_paths()
    {
        // 1. Chuỗi lành tính chứa timezone Asia/Ho_Chi_Minh phải được giữ nguyên
        const string benignMessage = "Tác vụ chạy tại múi giờ Asia/Ho_Chi_Minh hợp lệ.";
        var preserved = AwsErrorMapping.SanitizeForClient(benignMessage, errorCode: null);
        Assert.AreEqual(benignMessage, preserved, "Benign timezone Asia/Ho_Chi_Minh must not be falsely sanitized as AWS access key.");

        // 2. Đường dẫn /opt/app/... phải bị lọc khử trùng
        const string optPathMessage = "Lỗi đọc cấu hình tại /opt/app/config.json";
        var sanitized = AwsErrorMapping.SanitizeForClient(optPathMessage, AwsErrorCodes.S3FileNotFound);
        Assert.IsFalse(sanitized.Contains("/opt/app", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(sanitized.Contains("Không tìm thấy file", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 2. Stale Lease Recovery Tests (Process Crash / Abandoned Worker)

    [TestMethod]
    public async Task StaleLeaseRecovery_recovers_abandoned_job_to_pending_with_actionable_reason()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(clock: () => now);

        var request = JobRequest.Create(
            "ExamGeneration",
            "s3://bucket/blueprint.json",
            "teacher-01",
            SchoolScope,
            "idem-key-crash-01",
            "corr-crash-01");

        var job = await store.EnqueueAsync(request, "fingerprint-crash-01");
        var claim = await store.ClaimNextJobAsync("worker-crashed-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);
        Assert.AreEqual(JobStatus.Running, claim.Job.Status);

        // Giả lập worker bị sập (kill -9) và 3 phút trôi qua không có heartbeat
        now = now.AddMinutes(3);

        var recoveredCount = await store.RecoverExpiredLeasesAsync();
        Assert.AreEqual(1, recoveredCount, "Exactly 1 abandoned job should be recovered.");

        var recoveredJob = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(recoveredJob);
        Assert.AreEqual(JobStatus.Pending, recoveredJob.Status, "Recovered job must be rescheduled to Pending.");
        Assert.AreEqual(AwsErrorCodes.StaleLeaseRecovered, recoveredJob.ErrorCode);
        Assert.IsTrue(recoveredJob.SafeErrorMessage!.Contains("Worker Timeout", StringComparison.OrdinalIgnoreCase) ||
                      recoveredJob.SafeErrorMessage.Contains("mất kết nối", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(recoveredJob.AvailableAt, "AvailableAt must be set for retry.");

        // Kiểm tra lịch sử attempt cũ bị đánh dấu Abandoned
        var attempts = await store.GetAttemptsAsync(job.Id);
        Assert.HasCount(1, attempts);
        Assert.AreEqual(AttemptStatus.Abandoned, attempts[0].Status);
        Assert.IsNotNull(attempts[0].ExecutionDurationMs);
        Assert.AreEqual(AwsErrorCodes.StaleLeaseRecovered, attempts[0].ErrorCode);
    }

    [TestMethod]
    public async Task StaleLeaseRecovery_marks_failed_when_max_attempts_exhausted()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(clock: () => now);

        var request = JobRequest.Create(
            "ExamGeneration",
            "s3://bucket/blueprint.json",
            "teacher-01",
            SchoolScope,
            "idem-key-crash-max",
            "corr-crash-max");

        var job = await store.EnqueueAsync(request, "fingerprint-crash-max");

        // Lần 1: claim, crash, recover
        var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim1);
        now = now.AddMinutes(3);
        await store.RecoverExpiredLeasesAsync();

        // Lần 2: thời gian cần vượt qua AvailableAt (+1m)
        now = now.AddMinutes(2);
        var claim2 = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim2);
        now = now.AddMinutes(3);
        await store.RecoverExpiredLeasesAsync();

        // Lần 3: thời gian cần vượt qua AvailableAt (+5m)
        now = now.AddMinutes(6);
        var claim3 = await store.ClaimNextJobAsync("worker-3", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim3);
        now = now.AddMinutes(3);
        await store.RecoverExpiredLeasesAsync();

        var finalJob = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(finalJob);
        Assert.AreEqual(JobStatus.Failed, finalJob.Status, "Job must fail permanently after 3 crashed attempts.");
        Assert.AreEqual("RETRY_BUDGET_EXHAUSTED", finalJob.ErrorCode);
        Assert.IsTrue(finalJob.SafeErrorMessage!.Contains("tối đa 3 lần", StringComparison.OrdinalIgnoreCase) ||
                      finalJob.SafeErrorMessage.Contains("attempt limit", StringComparison.OrdinalIgnoreCase) ||
                      finalJob.SafeErrorMessage.Contains("hạn mức", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 3. AI Provider Circuit Breaker Tests

    [TestMethod]
    public void CircuitBreaker_trips_to_open_after_5_consecutive_transient_failures()
    {
        var now = DateTimeOffset.UtcNow;
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 5, coolingPeriod: TimeSpan.FromSeconds(60), clock: () => now);

        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));

        // 4 lỗi liên tiếp -> vẫn Closed
        for (int i = 0; i < 4; i++)
        {
            breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
            Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
            Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
        }

        // Lỗi thứ 5 -> ngắt mạch (Open)
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        Assert.AreEqual(CircuitBreakerState.Open, breaker.GetState("AWS_BEDROCK"));
        Assert.IsFalse(breaker.CanExecute("AWS_BEDROCK"), "Circuit breaker must reject calls when Open.");
    }

    [TestMethod]
    public void CircuitBreaker_transitions_to_half_open_after_cooldown_and_resets_on_success()
    {
        var now = DateTimeOffset.UtcNow;
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 3, coolingPeriod: TimeSpan.FromSeconds(60), clock: () => now);

        // Kích hoạt Open
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockModelTimeout);
        }
        Assert.AreEqual(CircuitBreakerState.Open, breaker.GetState("AWS_BEDROCK"));

        // Chưa hết 60s -> vẫn Open
        now = now.AddSeconds(30);
        Assert.IsFalse(breaker.CanExecute("AWS_BEDROCK"));

        // Hết 60s -> HalfOpen (cho phép 1 probe)
        now = now.AddSeconds(31);
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"), "Probe request must be allowed after cooling period.");
        Assert.AreEqual(CircuitBreakerState.HalfOpen, breaker.GetState("AWS_BEDROCK"));

        // Probe thành công -> Reset về Closed
        breaker.RecordSuccess("AWS_BEDROCK");
        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
    }

    [TestMethod]
    public void CircuitBreaker_ignores_client_validation_errors()
    {
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 3);

        // Lỗi do client gửi dữ liệu sai không được làm sập mạch bảo vệ AI
        for (int i = 0; i < 10; i++)
        {
            breaker.RecordFailure("AWS_BEDROCK", "ROW_VALIDATION_ERROR");
        }

        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
    }

    [TestMethod]
    public void CircuitBreaker_half_open_probe_released_on_permanent_error()
    {
        var now = DateTimeOffset.UtcNow;
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 1, coolingPeriod: TimeSpan.FromSeconds(60), clock: () => now);

        // Lỗi 1 -> Open
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        Assert.AreEqual(CircuitBreakerState.Open, breaker.GetState("AWS_BEDROCK"));

        // Hết cooldown 60s -> HalfOpen (cấp probe 1)
        now = now.AddSeconds(61);
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
        Assert.AreEqual(CircuitBreakerState.HalfOpen, breaker.GetState("AWS_BEDROCK"));

        // Probe gặp lỗi Permanent (ví dụ AI_MALFORMED_OUTPUT)
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.AiMalformedOutput);

        // Cờ HalfOpenProbeInFlight phải được giải phóng, cho phép probe tiếp theo thăm dò mà không kẹt
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"), "Probe must be released after non-provider permanent failure.");
    }

    [TestMethod]
    public void CircuitBreaker_half_open_probe_recovers_after_probe_timeout()
    {
        var now = DateTimeOffset.UtcNow;
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 1, coolingPeriod: TimeSpan.FromSeconds(30), clock: () => now);

        // Lỗi 1 -> Open
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        now = now.AddSeconds(31);

        // Cấp probe 1
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
        // Gọi lại ngay lập tức -> bị chặn vì probe đang xử lý
        Assert.IsFalse(breaker.CanExecute("AWS_BEDROCK"));

        // Giả lập worker giữ probe bị crash/hủy và quá thời gian cooling period (35s)
        now = now.AddSeconds(35);

        // Breaker phải cho phép probe mới thay thế probe bị bỏ rơi
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"), "New probe must be permitted after previous probe times out.");
    }

    [TestMethod]
    public void CircuitBreaker_resets_failures_outside_sliding_window()
    {
        var now = DateTimeOffset.UtcNow;
        var breaker = new InMemoryCircuitBreaker(
            failureThreshold: 5,
            coolingPeriod: TimeSpan.FromSeconds(60),
            clock: () => now,
            failureWindow: TimeSpan.FromSeconds(60));

        // 4 lỗi liên tiếp trong vòng 30s
        for (int i = 0; i < 4; i++)
        {
            breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
            now = now.AddSeconds(10);
        }
        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));

        // Chờ quá cửa sổ 60s (thời gian đã trôi qua 70s kể từ lỗi đầu tiên)
        now = now.AddSeconds(40);

        // Ghi nhận lỗi thứ 5 nhưng đã ngoài cửa sổ -> cửa sổ được reset, lỗi này chỉ tính là lỗi 1 của chu kỳ mới
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);

        // Mạch vẫn phải ở trạng thái Closed (không bị ngắt oan)
        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
    }

    #endregion

    #region 4. Retry Eligibility & JobService Tests

    [TestMethod]
    public async Task JobService_evaluates_retry_eligibility_correctly()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        // 1. Tác vụ bị hủy bởi người dùng (Cancelled) -> Eligible = true
        var req1 = JobRequest.Create("ExamGeneration", "payload-1", "user-01", SchoolScope, "key-elig-1", "corr-1");
        var job1 = await store.EnqueueAsync(req1, "fp-1");
        await store.RequestCancellationAsync(job1.Id, "user-01");

        var status1 = await service.GetJobStatusAsync(JobRef.Create(job1.Id), "user-01", SchoolScope);
        Assert.IsTrue(status1.IsRetryEligible ?? false, "Cancelled job should be retry eligible.");
        Assert.IsNull(status1.ActionableReason, "Cancelled job without error should have null ActionableReason.");

        // 2. Tác vụ thất bại do Permanent error (AWS_BEDROCK_ACCESS_DENIED) -> Eligible = false
        var req2 = JobRequest.Create("ExamGeneration", "payload-2", "user-01", SchoolScope, "key-elig-2", "corr-2");
        var job2 = await store.EnqueueAsync(req2, "fp-2");
        var claim2 = await store.ClaimNextJobAsync("w1", TimeSpan.FromMinutes(2));
        await store.FailJobAsync(job2.Id, claim2!.LeaseToken, AwsErrorCodes.BedrockAccessDenied, "Access Denied", isTransient: false);

        var status2 = await service.GetJobStatusAsync(JobRef.Create(job2.Id), "user-01", SchoolScope);
        Assert.IsFalse(status2.IsRetryEligible ?? true, "Permanent error must NOT be retry eligible.");
        Assert.IsTrue(status2.ActionableReason!.Contains("IAM", StringComparison.OrdinalIgnoreCase));

        // 3. Tác vụ hoàn tất (Completed) -> ActionableReason = null và IsRetryEligible = false
        var req3 = JobRequest.Create("ExamGeneration", "payload-3", "user-01", SchoolScope, "key-elig-3", "corr-3");
        var job3 = await store.EnqueueAsync(req3, "fp-3");
        var claim3 = await store.ClaimNextJobAsync("w1", TimeSpan.FromMinutes(2));
        await store.CompleteJobAsync(job3.Id, claim3!.LeaseToken, "result-exam-123");

        var status3 = await service.GetJobStatusAsync(JobRef.Create(job3.Id), "user-01", SchoolScope);
        Assert.AreEqual(JobStatus.Completed, status3.Status);
        Assert.IsNull(status3.ActionableReason, "Completed job must have null ActionableReason.");
        Assert.IsFalse(status3.IsRetryEligible ?? true, "Completed job must not be retry eligible.");
    }

    [TestMethod]
    public async Task JobService_rejects_retry_when_job_has_permanent_error()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var req = JobRequest.Create("ExamGeneration", "payload", "user-01", SchoolScope, "key-reject", "corr-reject");
        var job = await store.EnqueueAsync(req, "fp-reject");
        var claim = await store.ClaimNextJobAsync("w1", TimeSpan.FromMinutes(2));
        await store.FailJobAsync(job.Id, claim!.LeaseToken, AwsErrorCodes.BedrockAccessDenied, "Access Denied", isTransient: false);

        // Gọi RetryJobAsync với lỗi vĩnh viễn phải ném ngoại lệ JobNotRetryEligibleException
        var ex = await Assert.ThrowsExactlyAsync<JobNotRetryEligibleException>(
            () => service.RetryJobAsync(JobRef.Create(job.Id), "user-01", SchoolScope));

        Assert.IsTrue(ex.Message.Contains("not eligible for retry", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region 5. Handler Failure Simulation Tests

    [TestMethod]
    public async Task ExamGenerationHandler_fast_fails_when_circuit_breaker_is_open()
    {
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 1);
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        Assert.AreEqual(CircuitBreakerState.Open, breaker.GetState("AWS_BEDROCK"));

        var handler = new ExamGenerationJobHandler(breaker);
        var request = JobRequest.Create("ExamGeneration", "s3://bucket/test.json", "user-01", SchoolScope, "key-cb", "corr-cb");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(AwsErrorCodes.CircuitBreakerOpen, result.ErrorCode);
        Assert.IsTrue(result.IsTransient);
    }

    [TestMethod]
    [DataRow("simulate-bedrock-throttled", AwsErrorCodes.BedrockThrottled, true)]
    [DataRow("simulate-bedrock-access-denied", AwsErrorCodes.BedrockAccessDenied, false)]
    [DataRow("simulate-bedrock-quota-exceeded", AwsErrorCodes.BedrockQuotaExceeded, false)]
    [DataRow("simulate-ai-malformed-output", AwsErrorCodes.AiMalformedOutput, false)]
    [DataRow("simulate-rag-empty", AwsErrorCodes.RagRetrievalEmpty, false)]
    public async Task ExamGenerationHandler_simulates_aws_bedrock_failures(string payloadTrigger, string expectedCode, bool expectedTransient)
    {
        var handler = new ExamGenerationJobHandler(allowFailureSimulation: true);
        var request = JobRequest.Create("ExamGeneration", $"s3://bucket/{payloadTrigger}.json", "user-01", SchoolScope, "k1", "c1");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(expectedCode, result.ErrorCode);
        Assert.AreEqual(expectedTransient, result.IsTransient);
    }

    [TestMethod]
    public async Task ExamGenerationHandler_ignores_simulation_when_flag_is_disabled()
    {
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 5);
        // Handler với allowFailureSimulation = false (mặc định cho production)
        var handler = new ExamGenerationJobHandler(breaker, allowFailureSimulation: false);

        // Gửi 5 job chứa simulate-bedrock-throttled
        for (int i = 0; i < 5; i++)
        {
            var request = JobRequest.Create("ExamGeneration", "s3://bucket/simulate-bedrock-throttled.json", "user-01", SchoolScope, $"k-{i}", $"c-{i}");
            var context = new JobExecutionContext
            {
                JobId = JobRef.Create(Guid.NewGuid()),
                Request = request,
                AttemptNumber = 1
            };

            var result = await handler.ExecuteAsync(context, CancellationToken.None);
            Assert.IsTrue(result.Succeeded, "Job must proceed as normal success when failure simulation is disabled.");
        }

        // Breaker phải vẫn giữ nguyên trạng thái Closed, không bị mở mạch
        Assert.AreEqual(CircuitBreakerState.Closed, breaker.GetState("AWS_BEDROCK"));
        Assert.IsTrue(breaker.CanExecute("AWS_BEDROCK"));
    }

    [TestMethod]
    public async Task ExamGenerationHandler_returns_circuit_breaker_open_for_snapshot_payload_when_circuit_is_open()
    {
        var breaker = new InMemoryCircuitBreaker(failureThreshold: 1);
        breaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        Assert.AreEqual(CircuitBreakerState.Open, breaker.GetState("AWS_BEDROCK"));

        // Handler nhận payload s3://snapshot/x ở Attempt 2 nhưng breaker đang Open
        var handler = new ExamGenerationJobHandler(breaker, allowFailureSimulation: true);
        var request = JobRequest.Create("ExamGeneration", "s3://snapshot/exam-01.json", "user-01", SchoolScope, "key-snap-cb", "corr-snap-cb");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 2
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsFalse(result.Succeeded, "Client-provided s3://snapshot/ payload must NOT bypass circuit breaker.");
        Assert.AreEqual(AwsErrorCodes.CircuitBreakerOpen, result.ErrorCode);
        Assert.IsTrue(result.IsTransient);
    }

    [TestMethod]
    [DataRow("simulate-s3-access-denied", AwsErrorCodes.S3AccessDenied, false)]
    [DataRow("simulate-s3-file-not-found", AwsErrorCodes.S3FileNotFound, false)]
    [DataRow("simulate-s3-slowdown", AwsErrorCodes.S3SlowDown, true)]
    [DataRow("simulate-s3-timeout", AwsErrorCodes.S3Timeout, true)]
    public async Task CsvImportHandler_simulates_aws_s3_failures(string payloadTrigger, string expectedCode, bool expectedTransient)
    {
        var handler = new CsvImportJobHandler(allowFailureSimulation: true);
        var request = JobRequest.Create("CsvImport", $"s3://bucket/{payloadTrigger}.csv", "user-01", SchoolScope, "k2", "c2");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(expectedCode, result.ErrorCode);
        Assert.AreEqual(expectedTransient, result.IsTransient);
    }

    [TestMethod]
    public async Task CsvImportHandler_ignores_simulation_when_flag_is_disabled()
    {
        var handler = new CsvImportJobHandler(allowFailureSimulation: false);
        var request = JobRequest.Create("CsvImport", "s3://bucket/simulate-s3-file-not-found.csv", "user-01", SchoolScope, "k-ignore", "c-ignore");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, "CsvImport must succeed normally when failure simulation is disabled.");
        Assert.IsNotNull(result.ResultId);
    }

    #endregion
}
