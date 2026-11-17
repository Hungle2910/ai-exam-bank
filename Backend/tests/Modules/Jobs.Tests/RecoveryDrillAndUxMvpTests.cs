using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AiExamBank.Modules.Jobs.Tests;

/// <summary>
/// Bộ kiểm thử diễn tập phục hồi sự cố và nghiệm thu cổng Jobs UX MVP (M5-06 — Issue #30).
/// Kiểm chứng 5 kịch bản sự cố chuẩn, tính nhất quán trạng thái sau khi khởi động lại (Restart Consistency),
/// liên kết kết quả trực tiếp (ResultUrl) và rào chắn phân quyền khi thử lại.
/// </summary>
[TestClass]
public sealed class RecoveryDrillAndUxMvpTests
{
    private static readonly JobScope SchoolHn01 = JobScope.Create(JobScopeKind.School, "school-hn-01");
    private static readonly JobScope SchoolHcm02 = JobScope.Create(JobScopeKind.School, "school-hcm-02");

    [TestMethod]
    public async Task Drill01_WorkerCrash_StaleLeaseRecovery_RecoversJob_FencesZombieWorker_AndExposesRecoveryUx()
    {
        var currentTime = new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero);
        var metrics = new JobMetrics();
        var store = new InMemoryJobStore(() => currentTime, metrics: metrics);
        var service = new JobService(store, metrics);

        // 1. Enqueue ExamGeneration job
        var jobRef = await service.EnqueueAsync(JobRequest.Create(
            "ExamGeneration",
            "s3://ai-exam-bank/blueprints/math-grade12.json",
            "teacher-01",
            SchoolHn01,
            "drill01-idem-001",
            "corr-drill-01"));

        // 2. Worker 1 claims job, then crashes mid-execution
        var claim1 = await store.ClaimNextJobAsync("worker-crash-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim1);
        Assert.AreEqual(1, claim1.Attempt.AttemptNumber);

        await store.UpdateProgressAsync(jobRef.Value, claim1.LeaseToken, 50);

        // 3. Advance clock past lease expiration -> Recovery daemon reclaims stale lease
        currentTime = currentTime.AddMinutes(3);
        var recoveredCount = await store.RecoverExpiredLeasesAsync();
        Assert.AreEqual(1, recoveredCount);

        var pendingStatus = await service.GetJobStatusAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.AreEqual(JobStatus.Pending, pendingStatus.Status);
        Assert.AreEqual(AwsErrorCodes.StaleLeaseRecovered, pendingStatus.ErrorCode);
        Assert.AreEqual(1, pendingStatus.AttemptCount);
        Assert.IsTrue(pendingStatus.IsRecoveredFromFailure);
        Assert.IsNotNull(pendingStatus.RetryReason);
        Assert.IsNull(pendingStatus.ResultUrl);

        // 4. Advance clock past 1-minute retry delay -> Worker 2 claims and completes Attempt #2
        currentTime = currentTime.AddMinutes(2);
        var claim2 = await store.ClaimNextJobAsync("worker-healthy-02", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim2);
        Assert.AreEqual(2, claim2.Attempt.AttemptNumber);

        // Zombie Worker 1 wakes up late and tries to overwrite using old lease token -> MUST BE REJECTED (Fencing)
        var zombieComplete = await store.CompleteJobAsync(jobRef.Value, claim1.LeaseToken, "corrupted-snapshot");
        Assert.IsFalse(zombieComplete, "Zombie worker with expired fencing token must be rejected.");

        // Healthy Worker 2 completes the job
        var expectedSnapshotId = $"exam-snap-{jobRef.Value:N}";
        var healthyComplete = await store.CompleteJobAsync(jobRef.Value, claim2.LeaseToken, expectedSnapshotId);
        Assert.IsTrue(healthyComplete);

        // 5. Verify Jobs UX MVP fields after recovery
        var finalStatus = await service.GetJobStatusAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.AreEqual(JobStatus.Completed, finalStatus.Status);
        Assert.AreEqual(2, finalStatus.AttemptCount);
        Assert.IsTrue(finalStatus.IsRecoveredFromFailure);
        Assert.AreEqual(expectedSnapshotId, finalStatus.ResultId);
        Assert.AreEqual($"/exams/preview?snapshotId={expectedSnapshotId}", finalStatus.ResultUrl);
        Assert.IsNotNull(finalStatus.UpdatedAt);

        var attempts = await service.GetJobAttemptsAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.HasCount(2, attempts);
        Assert.AreEqual(AttemptStatus.Abandoned, attempts[0].Status);
        Assert.AreEqual(AttemptStatus.Completed, attempts[1].Status);
    }

    [TestMethod]
    public async Task Drill02_TimeoutAfterWrite_ExamGenerationAndCsvImport_ReusesResult_WithoutDuplicateCreation()
    {
        var examHandler = new ExamGenerationJobHandler(allowFailureSimulation: true);
        var csvHandler = new CsvImportJobHandler(allowFailureSimulation: true);

        var examJobId = JobRef.Create(Guid.NewGuid());
        var csvJobId = JobRef.Create(Guid.NewGuid());

        var examRequest = JobRequest.Create(
            "ExamGeneration",
            "s3://ai-exam-bank/blueprints/simulate-timeout-after-write.json",
            "teacher-01",
            SchoolHn01,
            "drill02-exam-idem",
            "corr-drill-02-exam");

        var csvRequest = JobRequest.Create(
            "CsvImport",
            "s3://ai-exam-bank/imports/simulate-timeout-after-write.csv",
            "teacher-01",
            SchoolHn01,
            "drill02-csv-idem",
            "corr-drill-02-csv");

        // Attempt #2 after simulated timeout-after-write on Attempt #1 -> Must reuse deterministic ResultId
        var examResultAttempt2 = await examHandler.ExecuteAsync(
            new JobExecutionContext
            {
                JobId = examJobId,
                Request = examRequest,
                AttemptNumber = 2
            },
            CancellationToken.None);

        var csvResultAttempt2 = await csvHandler.ExecuteAsync(
            new JobExecutionContext
            {
                JobId = csvJobId,
                Request = csvRequest,
                AttemptNumber = 2
            },
            CancellationToken.None);

        Assert.IsTrue(examResultAttempt2.Succeeded);
        Assert.AreEqual($"exam-snap-{examJobId.Value:N}", examResultAttempt2.ResultId);

        Assert.IsTrue(csvResultAttempt2.Succeeded);
        Assert.AreEqual($"import-batch-{csvJobId.Value:N}", csvResultAttempt2.ResultId);

        // Verify ResultUrl mapping for both ExamGeneration and CsvImport
        var examUrl = JobService.BuildResultUrl("ExamGeneration", examJobId.Value, examResultAttempt2.ResultId, JobStatus.Completed);
        var csvUrl = JobService.BuildResultUrl("CsvImport", csvJobId.Value, csvResultAttempt2.ResultId, JobStatus.Completed);

        Assert.AreEqual($"/exams/preview?snapshotId=exam-snap-{examJobId.Value:N}", examUrl);
        Assert.AreEqual($"/questions?importBatchId=import-batch-{csvJobId.Value:N}", csvUrl);
    }

    [TestMethod]
    public async Task Drill03_ProviderThrottlingAndCircuitBreaker_TripsOn5Failures_DefersJobs_AndRecoversViaHalfOpen()
    {
        var currentTime = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
        var metrics = new JobMetrics();
        var snsOptions = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            TopicArn = "arn:aws:sns:ap-southeast-1:123456789012:ai-exam-bank-alerts",
            EnvironmentName = "Staging",
            ThrottleWindowSeconds = 300
        };
        InMemoryJobStore? storeRef = null;
        var alertPublisher = new SnsAlertPublisher(snsOptions, null, () => storeRef);
        var store = new InMemoryJobStore(() => currentTime, metrics: metrics, alertPublisher: alertPublisher);
        storeRef = store;

        var circuitBreaker = new InMemoryCircuitBreaker(
            failureThreshold: 5,
            coolingPeriod: TimeSpan.FromSeconds(60),
            clock: () => currentTime,
            metrics: metrics,
            alertPublisher: alertPublisher,
            store: store);

        var handler = new ExamGenerationJobHandler(circuitBreaker, allowFailureSimulation: true);
        var service = new JobService(store, metrics, alertPublisher);

        // 1. Trigger 5 consecutive Bedrock 429 throttling failures -> Trips Circuit Breaker to Open
        for (int i = 1; i <= 5; i++)
        {
            var req = JobRequest.Create(
                "ExamGeneration",
                "s3://ai-exam-bank/blueprints/simulate-bedrock-throttled.json",
                "teacher-01",
                SchoolHn01,
                $"drill03-throttle-{i}",
                $"corr-drill03-{i}");
            var jRef = await service.EnqueueAsync(req);
            var res = await handler.ExecuteAsync(
                new JobExecutionContext
                {
                    JobId = jRef,
                    Request = req,
                    AttemptNumber = 1
                },
                CancellationToken.None);
            Assert.IsFalse(res.Succeeded);
            Assert.AreEqual(AwsErrorCodes.BedrockThrottled, res.ErrorCode);
        }

        Assert.AreEqual(CircuitBreakerState.Open, circuitBreaker.GetState("AWS_BEDROCK"));

        // 2. 6th job arrives while Circuit is Open -> Deferred safely (AttemptCount rolled back, remains Pending)
        var deferredReq = JobRequest.Create(
            "ExamGeneration",
            "s3://ai-exam-bank/blueprints/normal-blueprint.json",
            "teacher-01",
            SchoolHn01,
            "drill03-deferred-6",
            "corr-drill03-6");
        var deferredRef = await service.EnqueueAsync(deferredReq);
        var claim6 = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim6);

        var openRes = await handler.ExecuteAsync(
            new JobExecutionContext
            {
                JobId = deferredRef,
                Request = deferredReq,
                AttemptNumber = claim6.Attempt.AttemptNumber
            },
            CancellationToken.None);
        Assert.AreEqual(AwsErrorCodes.CircuitBreakerOpen, openRes.ErrorCode);

        await store.FailJobAsync(deferredRef.Value, claim6.LeaseToken, openRes.ErrorCode!, openRes.SafeErrorMessage!, openRes.IsTransient);
        var deferredJob = await store.GetByIdAsync(deferredRef.Value);
        Assert.IsNotNull(deferredJob);
        Assert.AreEqual(JobStatus.Pending, deferredJob.Status);
        Assert.AreEqual(0, deferredJob.AttemptCount, "CircuitBreakerOpen deferral must not consume retry budget.");

        // 3. Advance clock past 60s cooldown -> HalfOpen probe succeeds -> Circuit closes
        currentTime = currentTime.AddSeconds(65);
        Assert.AreEqual(CircuitBreakerState.HalfOpen, circuitBreaker.GetState("AWS_BEDROCK"));

        var claimProbe = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claimProbe);
        var probeRes = await handler.ExecuteAsync(
            new JobExecutionContext
            {
                JobId = deferredRef,
                Request = deferredReq,
                AttemptNumber = claimProbe.Attempt.AttemptNumber
            },
            CancellationToken.None);
        Assert.IsTrue(probeRes.Succeeded);
        await store.CompleteJobAsync(deferredRef.Value, claimProbe.LeaseToken, probeRes.ResultId!);

        Assert.AreEqual(CircuitBreakerState.Closed, circuitBreaker.GetState("AWS_BEDROCK"));
    }

    [TestMethod]
    public async Task Drill04_PermanentValidationAndAccessDenied_FailsImmediately_LocksRetry_AndRedactsSecrets()
    {
        var currentTime = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var store = new InMemoryJobStore(() => currentTime);
        var service = new JobService(store);

        var jobRef = await service.EnqueueAsync(JobRequest.Create(
            "CsvImport",
            "s3://ai-exam-bank/imports/simulate-s3-access-denied.csv",
            "teacher-01",
            SchoolHn01,
            "drill04-perm-01",
            "corr-drill-04"));

        var claim = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Simulate raw diagnostic exception containing secrets (AKIA key, ARN, file path)
        var rawSensitiveError = "Amazon S3 403 AccessDenied using key AKIAIOSFODNN7EXAMPLE on arn:aws:iam::123456789012:role/ProdRole at C:\\secrets\\s3_client.cs:line 88";
        await store.FailJobAsync(
            jobRef.Value,
            claim.LeaseToken,
            AwsErrorCodes.S3AccessDenied,
            rawSensitiveError,
            isTransient: false);

        var status = await service.GetJobStatusAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.AreEqual(JobStatus.Failed, status.Status);
        Assert.IsFalse(status.IsRetryEligible ?? true, "Permanent S3AccessDenied error must lock retry eligibility.");
        Assert.IsNotNull(status.ActionableReason);

        // Verify CWE-209 Zero Information Leak
        Assert.IsFalse(status.SafeMessage!.Contains("AKIAIOSFODNN7EXAMPLE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(status.SafeMessage!.Contains("arn:aws:iam", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(status.SafeMessage!.Contains("C:\\secrets", StringComparison.OrdinalIgnoreCase));

        // Verify calling RetryJobAsync throws JobNotRetryEligibleException (HTTP 409 Conflict)
        await Assert.ThrowsExactlyAsync<JobNotRetryEligibleException>(async () =>
        {
            await service.RetryJobAsync(jobRef, "teacher-01", SchoolHn01, "Attempt forced retry");
        });
    }

    [TestMethod]
    public async Task Drill05_UserCancellationDuringExecution_CancelsCleanly_AndEnforcesScopedRetrySecurity()
    {
        var currentTime = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryJobStore(() => currentTime);
        var service = new JobService(store);

        var jobRef = await service.EnqueueAsync(JobRequest.Create(
            "ExamGeneration",
            "s3://ai-exam-bank/blueprints/physics-12.json",
            "teacher-01",
            SchoolHn01,
            "drill05-cancel-01",
            "corr-drill-05"));

        var claim = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Cross-scope cancellation attempt must be rejected (Zero-Trust Scope Security)
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.CancelJobAsync(jobRef, "teacher-01", SchoolHcm02);
        });

        // Legitimate owner cancels running job
        var cancelled = await service.CancelJobAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.IsTrue(cancelled);

        var statusAfterCancel = await service.GetJobStatusAsync(jobRef, "teacher-01", SchoolHn01);
        Assert.AreEqual(JobStatus.Cancelled, statusAfterCancel.Status);
        Assert.IsTrue(statusAfterCancel.IsRetryEligible ?? false);

        // Cross-scope retry attempt must be rejected
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.RetryJobAsync(jobRef, "teacher-01", SchoolHcm02, "Cross-scope hijack");
        });

        // Legitimate owner retries cancelled job -> Resets to Pending with RetryReason, no privilege escalation
        var retriedStatus = await service.RetryJobAsync(jobRef, "teacher-01", SchoolHn01, "Hủy nhầm, chạy lại tạo đề");
        Assert.AreEqual(JobStatus.Pending, retriedStatus.Status);
        Assert.AreEqual("Hủy nhầm, chạy lại tạo đề", retriedStatus.RetryReason);
        Assert.IsTrue(retriedStatus.IsRecoveredFromFailure);
        Assert.IsNull(retriedStatus.ResultId);
    }

    [TestMethod]
    public async Task RestartConsistency_FileJobStore_PreservesAllJobStatesAttemptsResultLinksAndTelemetry()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"m5_06_restart_consistency_{Guid.NewGuid():N}.json");
        try
        {
            var currentTime = new DateTimeOffset(2026, 11, 17, 11, 0, 0, TimeSpan.Zero);
            Guid recoveredJobId;
            string expectedSnapshotId;

            // Process Instance #1: Run job through Stale Lease Recovery -> Completion + Record Circuit Breaker & Alert
            {
                var store1 = new FileJobStore(tempFile, () => currentTime);
                var service1 = new JobService(store1);

                var jobRef = await service1.EnqueueAsync(JobRequest.Create(
                    "ExamGeneration",
                    "s3://ai-exam-bank/blueprints/chemistry-12.json",
                    "teacher-01",
                    SchoolHn01,
                    "restart-idem-01",
                    "corr-restart-01"));
                recoveredJobId = jobRef.Value;
                expectedSnapshotId = $"exam-snap-{recoveredJobId:N}";

                var claim1 = await store1.ClaimNextJobAsync("worker-before-restart", TimeSpan.FromMinutes(2));
                Assert.IsNotNull(claim1);

                // Simulate worker crash and lease expiration
                currentTime = currentTime.AddMinutes(3);
                var recovered = await store1.RecoverExpiredLeasesAsync();
                Assert.AreEqual(1, recovered);

                // Second attempt after recovery completes
                currentTime = currentTime.AddMinutes(2);
                var claim2 = await store1.ClaimNextJobAsync("worker-after-recovery", TimeSpan.FromMinutes(2));
                Assert.IsNotNull(claim2);
                await store1.CompleteJobAsync(recoveredJobId, claim2.LeaseToken, expectedSnapshotId);
                await store1.RecordCircuitBreakerStateAsync("AWS_BEDROCK", CircuitBreakerState.Closed);
            }

            // Process Instance #2 (Simulating full API + Worker restart reading from disk)
            {
                var store2 = new FileJobStore(tempFile, () => currentTime);
                var service2 = new JobService(store2);

                var reloadedStatus = await service2.GetJobStatusAsync(JobRef.Create(recoveredJobId), "teacher-01", SchoolHn01);
                Assert.AreEqual(JobStatus.Completed, reloadedStatus.Status);
                Assert.AreEqual(2, reloadedStatus.AttemptCount);
                Assert.IsTrue(reloadedStatus.IsRecoveredFromFailure);
                Assert.AreEqual("Tự động phục hồi sau khi Worker bị mất tín hiệu Heartbeat.", reloadedStatus.RetryReason);
                Assert.AreEqual(expectedSnapshotId, reloadedStatus.ResultId);
                Assert.AreEqual($"/exams/preview?snapshotId={expectedSnapshotId}", reloadedStatus.ResultUrl);

                var reloadedAttempts = await service2.GetJobAttemptsAsync(JobRef.Create(recoveredJobId), "teacher-01", SchoolHn01);
                Assert.HasCount(2, reloadedAttempts);
                Assert.AreEqual(AttemptStatus.Abandoned, reloadedAttempts[0].Status);
                Assert.AreEqual(AttemptStatus.Completed, reloadedAttempts[1].Status);

                var summary = await service2.GetReliabilitySummaryAsync();
                Assert.AreEqual(1, summary.TotalCompleted);
                Assert.AreEqual(1, summary.StaleLeaseRecoveries);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
