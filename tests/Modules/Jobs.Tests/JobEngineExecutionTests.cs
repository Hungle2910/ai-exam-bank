using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using AiExamBank.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiExamBank.Modules.Jobs.Tests;

[TestClass]
public sealed class JobEngineExecutionTests
{
    private static readonly JobScope SchoolScope = JobScope.Create(JobScopeKind.School, "school-hn-01");

    [TestMethod]
    public async Task Enqueue_returns_same_job_on_identical_fingerprint()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-1", "corr-1");

        var job1 = await store.EnqueueAsync(request, "fingerprint-abc");
        var job2 = await store.EnqueueAsync(request, "fingerprint-abc");

        Assert.AreEqual(job1.Id, job2.Id);
        Assert.AreEqual(JobStatus.Pending, job2.Status);
    }

    [TestMethod]
    public async Task Enqueue_throws_conflict_on_mismatched_fingerprint()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-1", "corr-1");

        await store.EnqueueAsync(request, "fingerprint-abc");

        await Assert.ThrowsExactlyAsync<JobConflictException>(async () =>
        {
            await store.EnqueueAsync(request, "fingerprint-different-xyz");
        });
    }

    [TestMethod]
    public async Task Competing_workers_only_one_claims_job()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-2", "corr-2");
        var job = await store.EnqueueAsync(request, "fp-1");

        var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        var claim2 = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));

        Assert.IsNotNull(claim1);
        Assert.AreEqual(job.Id, claim1.Job.Id);
        Assert.AreEqual("worker-1", claim1.Attempt.WorkerId);

        // Worker 2 nhận null vì không còn job nào khả dụng
        Assert.IsNull(claim2);
    }

    [TestMethod]
    public async Task Heartbeat_renews_for_active_lease_and_fences_stale_tokens()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-3", "corr-3");
        var job = await store.EnqueueAsync(request, "fp-1");

        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Heartbeat thành công với LeaseToken đúng
        var renewed = await store.HeartbeatAsync(job.Id, claim.LeaseToken, TimeSpan.FromMinutes(2));
        Assert.IsTrue(renewed);

        // Fencing: Heartbeat thất bại với LeaseToken sai
        var staleLeaseToken = Guid.NewGuid();
        var fenced = await store.HeartbeatAsync(job.Id, staleLeaseToken, TimeSpan.FromMinutes(2));
        Assert.IsFalse(fenced);
    }

    [TestMethod]
    public async Task Fencing_prevents_stale_worker_from_completing_job()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-4", "corr-4");
        var job = await store.EnqueueAsync(request, "fp-1");

        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Giả lập worker cũ dùng token sai cố hoàn tất job
        var wrongToken = Guid.NewGuid();
        var completedByWrong = await store.CompleteJobAsync(job.Id, wrongToken, "fake-result");
        Assert.IsFalse(completedByWrong);

        // Worker hợp lệ hoàn tất thành công
        var completed = await store.CompleteJobAsync(job.Id, claim.LeaseToken, "exam-res-01");
        Assert.IsTrue(completed);

        var updated = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(JobStatus.Completed, updated.Status);
        Assert.AreEqual("exam-res-01", updated.ResultId);
    }

    [TestMethod]
    public async Task Transient_failure_schedules_bounded_retries_and_exhausts_at_attempt_3()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(() => now);
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-5", "corr-5");
        var job = await store.EnqueueAsync(request, "fp-1");

        // Attempt 1: Thất bại tạm thời -> quay về Pending hẹn giờ sau 1 phút
        var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim1);
        await store.FailJobAsync(job.Id, claim1.LeaseToken, "TIMEOUT", "Timeout 1", isTransient: true);

        var jobAfter1 = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(jobAfter1);
        Assert.AreEqual(JobStatus.Pending, jobAfter1.Status);
        Assert.AreEqual(1, jobAfter1.AttemptCount);
        Assert.AreEqual(now.AddMinutes(1), jobAfter1.AvailableAt);

        // Tua đồng hồ qua 1 phút
        now = now.AddMinutes(1).AddSeconds(1);

        // Attempt 2: Thất bại tạm thời -> quay về Pending hẹn giờ sau 5 phút
        var claim2 = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim2);
        await store.FailJobAsync(job.Id, claim2.LeaseToken, "TIMEOUT", "Timeout 2", isTransient: true);

        var jobAfter2 = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(jobAfter2);
        Assert.AreEqual(JobStatus.Pending, jobAfter2.Status);
        Assert.AreEqual(2, jobAfter2.AttemptCount);
        Assert.AreEqual(now.AddMinutes(5), jobAfter2.AvailableAt);

        // Tua đồng hồ qua 5 phút
        now = now.AddMinutes(5).AddSeconds(1);

        // Attempt 3: Thất bại tạm thời -> Đã chạm trần 3 lần -> Chuyển sang Failed vĩnh viễn
        var claim3 = await store.ClaimNextJobAsync("worker-3", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim3);
        await store.FailJobAsync(job.Id, claim3.LeaseToken, "TIMEOUT", "Timeout 3", isTransient: true);

        var jobAfter3 = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(jobAfter3);
        Assert.AreEqual(JobStatus.Failed, jobAfter3.Status);
        Assert.AreEqual(3, jobAfter3.AttemptCount);
        Assert.AreEqual("RETRY_BUDGET_EXHAUSTED", jobAfter3.ErrorCode);
    }

    [TestMethod]
    public async Task Cancellation_marks_pending_job_cancelled_immediately()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-6", "corr-6");
        var job = await store.EnqueueAsync(request, "fp-1");

        var cancelled = await store.RequestCancellationAsync(job.Id, "user-1");
        Assert.IsTrue(cancelled);

        var updated = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(JobStatus.Cancelled, updated.Status);
        Assert.IsNotNull(updated.CancellationRequestedAt);

        // Thao tác hủy lần 2 có tính Idempotent
        var repeatCancel = await store.RequestCancellationAsync(job.Id, "user-1");
        Assert.IsTrue(repeatCancel);
    }

    [TestMethod]
    public async Task Stale_lease_recovery_reclaims_abandoned_job()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(() => now);
        var request = JobRequest.Create("ExamGen", "s3://payload-1", "user-1", SchoolScope, "idem-7", "corr-7");
        var job = await store.EnqueueAsync(request, "fp-1");

        var claim = await store.ClaimNextJobAsync("worker-crashed", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Chưa quá hạn 2 phút -> Recover không làm gì
        var recoveredEarly = await store.RecoverExpiredLeasesAsync();
        Assert.AreEqual(0, recoveredEarly);

        // Tua đồng hồ quá 2 phút (Worker bị crash không gửi Heartbeat)
        now = now.AddMinutes(2).AddSeconds(5);

        // Quét thu hồi
        var recovered = await store.RecoverExpiredLeasesAsync();
        Assert.AreEqual(1, recovered);

        // Job được đưa về Pending để thử lại
        var updated = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(JobStatus.Pending, updated.Status);

        // Lượt trước bị đánh dấu Abandoned
        var attempts = store.GetAttempts(job.Id);
        Assert.HasCount(1, attempts);
        Assert.AreEqual(AttemptStatus.Abandoned, attempts[0].Status);
    }

    [TestMethod]
    public async Task Job_service_enqueue_and_tracker_list_respects_scope()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var req1 = JobRequest.Create("ExamGen", "s3://ref-1", "teacher-1", SchoolScope, "key-1", "c-1");
        var req2 = JobRequest.Create("ExamGen", "s3://ref-2", "teacher-2", SchoolScope, "key-2", "c-2");
        var reqOtherSchool = JobRequest.Create("ExamGen", "s3://ref-3", "teacher-3",
            JobScope.Create(JobScopeKind.School, "school-sg-02"), "key-3", "c-3");

        var jobRef1 = await service.EnqueueAsync(req1);
        var jobRef2 = await service.EnqueueAsync(req2);
        await service.EnqueueAsync(reqOtherSchool);

        var schoolList = await service.ListJobsAsync(SchoolScope, "teacher-1", requesterScope: SchoolScope);
        Assert.HasCount(2, schoolList);

        var status1 = await service.GetJobStatusAsync(jobRef1, "teacher-1", requesterScope: SchoolScope);
        Assert.AreEqual(JobStatus.Pending, status1.Status);
    }

    [TestMethod]
    public async Task Worker_service_executes_sample_handler_to_completion()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        registry.Register(new SampleJobHandler());

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(5)
        });

        var request = JobRequest.Create("SampleJob", "s3://sample-payload", "tester", SchoolScope, "key-sample", "corr-sample");
        var job = await store.EnqueueAsync(request, "fp-sample");

        using var workerCts = new CancellationTokenSource();
        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);

        // Khởi động worker trong nền
        var workerTask = worker.StartAsync(workerCts.Token);

        // Chờ tác vụ hoàn thành (tối đa 3 giây)
        BackgroundJob? completedJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(100);
            var current = await store.GetByIdAsync(job.Id);
            if (current != null && current.Status == JobStatus.Completed)
            {
                completedJob = current;
                break;
            }
        }

        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.IsNotNull(completedJob, "Job did not complete in expected time window.");
        Assert.AreEqual(JobStatus.Completed, completedJob.Status);
        Assert.AreEqual(100, completedJob.ProgressPercentage);
        Assert.IsNotNull(completedJob.ResultId);
    }

    [TestMethod]
    public async Task Cross_school_read_is_denied_with_unauthorized_access_exception()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-scope-1", "corr-scope");
        var jobRef = await service.EnqueueAsync(request);

        // Truy cập cùng trường: Thành công
        var validStatus = await service.GetJobStatusAsync(jobRef, "teacher-hn", requesterScope: SchoolScope);
        Assert.IsNotNull(validStatus);

        // Truy cập chéo trường khác: Bị từ chối (403 Forbidden)
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.GetJobStatusAsync(jobRef, "teacher-sg", requesterScope: JobScope.Create(JobScopeKind.School, "school-sg-02"));
        });
    }

    [TestMethod]
    public async Task Missing_scope_on_read_or_cancel_or_list_throws_unauthorized_access_exception()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-miss-1", "corr-miss");
        var jobRef = await service.EnqueueAsync(request);

        // 1. Thiếu scope trên status check: Bị từ chối ngay lập tức
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.GetJobStatusAsync(jobRef, "teacher-hn", requesterScope: null!);
        });

        // 2. Thiếu scope trên cancel: Bị từ chối ngay lập tức
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.CancelJobAsync(jobRef, "teacher-hn", requesterScope: null!);
        });

        // 3. Thiếu scope trên list: Bị từ chối ngay lập tức
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ListJobsAsync(SchoolScope, "teacher-hn", requesterScope: null!);
        });

        // 4. Mismatched scope trên list: Bị từ chối ngay lập tức
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ListJobsAsync(SchoolScope, "teacher-hn", requesterScope: JobScope.Create(JobScopeKind.School, "school-diff-99"));
        });
    }

    [TestMethod]
    public async Task Cross_scope_kind_access_with_matching_resource_id_is_rejected()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var schoolScope = JobScope.Create(JobScopeKind.School, "resource-01");
        var ministryScope = JobScope.Create(JobScopeKind.MinistryExam, "resource-01");

        var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", schoolScope, "idem-cross-kind-1", "corr-kind");
        var jobRef = await service.EnqueueAsync(request);

        // 1. GetJobStatusAsync: requester có MinistryExam với cùng resourceId "resource-01" không được phép đọc job School
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.GetJobStatusAsync(jobRef, "officer", requesterScope: ministryScope);
        });

        // 2. CancelJobAsync: requester có MinistryExam với cùng resourceId không được phép hủy job School
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.CancelJobAsync(jobRef, "officer", requesterScope: ministryScope);
        });

        // 3. ListJobsAsync: requester có MinistryExam không được phép list job School
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ListJobsAsync(schoolScope, "officer", requesterScope: ministryScope);
        });

        // 4. Đúng scope (cả Kind và ResourceId): thành công
        var status = await service.GetJobStatusAsync(jobRef, "teacher-hn", requesterScope: schoolScope);
        Assert.IsNotNull(status);
    }

    [TestMethod]
    public async Task Case_insensitive_resource_id_in_store_list_returns_matching_jobs()
    {
        var inMemoryStore = new InMemoryJobStore();
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobstore_case_test_{Guid.NewGuid():N}.json");
        try
        {
            var fileStore = new FileJobStore(tempFile);

            var reqLower = JobRequest.Create("ExamGen", "s3://ref", "teacher-1",
                JobScope.Create(JobScopeKind.School, "school-hn-01"), "idem-c-1", "corr-c-1");

            await inMemoryStore.EnqueueAsync(reqLower, "fp-c-1");
            await fileStore.EnqueueAsync(reqLower, "fp-c-1");

            // Truy vấn với chữ hoa "SCHOOL-HN-01"
            var queryScopeUpper = JobScope.Create(JobScopeKind.School, "SCHOOL-HN-01");

            var inMemoryList = await inMemoryStore.ListByScopeAsync(queryScopeUpper);
            Assert.HasCount(1, inMemoryList);

            var fileList = await fileStore.ListByScopeAsync(queryScopeUpper);
            Assert.HasCount(1, fileList);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task Case_insensitive_resource_id_creates_same_idempotency_and_fingerprint()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var req1 = JobRequest.Create("ExamGen", "s3://ref", "teacher-1",
            JobScope.Create(JobScopeKind.School, "school-hn-01"), "shared-idem-key", "corr-1");
        var req2 = JobRequest.Create("ExamGen", "s3://ref", "teacher-1",
            JobScope.Create(JobScopeKind.School, "SCHOOL-HN-01"), "shared-idem-key", "corr-2");

        var jobRef1 = await service.EnqueueAsync(req1);
        var jobRef2 = await service.EnqueueAsync(req2);

        // Hai yêu cầu chỉ khác nhau chữ hoa/thường ở ResourceId phải được deduplicate về cùng một Job
        Assert.AreEqual(jobRef1.Value, jobRef2.Value);
    }

    [TestMethod]
    public async Task UpdateProgress_or_complete_with_expired_lease_fails_even_with_valid_token()
    {
        var currentTime = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(() => currentTime);

        var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-lease-1", "corr-lease");
        var job = await store.EnqueueAsync(request, "fp-lease");

        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Giả lập thời gian trôi qua 3 phút (> 2 phút lease)
        currentTime = currentTime.AddMinutes(3);

        // Worker cũ cố cập nhật tiến độ hoặc hoàn thành -> Bị Fencing chặn đứng
        var progressUpdated = await store.UpdateProgressAsync(job.Id, claim.LeaseToken, 50);
        Assert.IsFalse(progressUpdated, "UpdateProgress with expired lease should fail.");

        var heartbeatSucceeded = await store.HeartbeatAsync(job.Id, claim.LeaseToken, TimeSpan.FromMinutes(2));
        Assert.IsFalse(heartbeatSucceeded, "Heartbeat with expired lease should fail.");

        var completed = await store.CompleteJobAsync(job.Id, claim.LeaseToken, "res-1");
        Assert.IsFalse(completed, "Complete with expired lease should fail.");
    }

    [TestMethod]
    public async Task FileJobStore_preserves_queued_jobs_across_restart_and_instances()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobs_test_{Guid.NewGuid():N}.json");

        try
        {
            // Instance 1: Đẩy job vào hàng đợi
            using (var store1 = new FileJobStore(tempFile))
            {
                var request = JobRequest.Create("SampleJob", "s3://temp-ref", "user-1", SchoolScope, "key-persist-1", "corr-1");
                var enqueued = await store1.EnqueueAsync(request, "fp-1");
                Assert.AreEqual(JobStatus.Pending, enqueued.Status);
            }

            // Instance 2: Giả lập restart tiến trình - đọc lại từ tệp
            using (var store2 = new FileJobStore(tempFile))
            {
                var list = await store2.ListByScopeAsync(SchoolScope);
                Assert.HasCount(1, list, "Queued job must be preserved across restart.");
                Assert.AreEqual(JobStatus.Pending, list[0].Status);

                // Claim và thực thi hoàn tất
                var claim = await store2.ClaimNextJobAsync("worker-restart", TimeSpan.FromMinutes(2));
                Assert.IsNotNull(claim);
                Assert.AreEqual(JobStatus.Running, claim.Job.Status);

                var completed = await store2.CompleteJobAsync(claim.Job.Id, claim.LeaseToken, "res-restart-100");
                Assert.IsTrue(completed);
            }

            // Instance 3: Xác minh trạng thái đã hoàn tất được lưu bền vững trên đĩa
            using (var store3 = new FileJobStore(tempFile))
            {
                var list = await store3.ListByScopeAsync(SchoolScope);
                Assert.HasCount(1, list);
                Assert.AreEqual(JobStatus.Completed, list[0].Status);
                Assert.AreEqual("res-restart-100", list[0].ResultId);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [TestMethod]
    public async Task Exam_generation_and_csv_import_handlers_are_registered_and_executable()
    {
        var registry = new JobRegistry();
        registry.Register(new ExamGenerationJobHandler());
        registry.Register(new CsvImportJobHandler());

        Assert.IsTrue(registry.HasHandler("ExamGeneration"));
        Assert.IsTrue(registry.HasHandler("CsvImport"));
        Assert.HasCount(2, registry.RegisteredTypes);

        var reqExam = JobRequest.Create("ExamGeneration", "s3://blueprints/bp1.json", "m1-lead", SchoolScope, "k1", "c1");
        var ctxExam = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = reqExam,
            AttemptNumber = 1,
            ReportProgressAsync = _ => Task.CompletedTask
        };
        var resExam = await registry.GetHandler("ExamGeneration")!.ExecuteAsync(ctxExam, CancellationToken.None);
        Assert.IsTrue(resExam.Succeeded);
        Assert.IsNotNull(resExam.ResultId);

        var reqImport = JobRequest.Create("CsvImport", "s3://import/simulate-row-error.csv", "m2-devops", SchoolScope, "k2", "c2");
        var ctxImport = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = reqImport,
            AttemptNumber = 1,
            ReportProgressAsync = _ => Task.CompletedTask
        };
        var resImport = await registry.GetHandler("CsvImport")!.ExecuteAsync(ctxImport, CancellationToken.None);
        Assert.IsFalse(resImport.Succeeded);
        Assert.AreEqual("ROW_VALIDATION_ERROR", resImport.ErrorCode);
    }

    [TestMethod]
    public void Job_scope_create_throws_argument_exception_on_whitespace_or_undefined_enum()
    {
        // 1. Whitespace resourceId throws ArgumentException
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            JobScope.Create(JobScopeKind.School, "   ");
        });

        // 2. Undefined enum kind throws ArgumentOutOfRangeException (derived from ArgumentException)
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
        {
            JobScope.Create((JobScopeKind)999, "school-hn-01");
        });
    }

    [TestMethod]
    public async Task Job_service_list_cross_school_throws_unauthorized_access_exception()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);

        var schoolHn = JobScope.Create(JobScopeKind.School, "school-hn-01");
        var schoolSg = JobScope.Create(JobScopeKind.School, "school-sg-02");

        var req = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", schoolHn, "k-cross", "c-cross");
        await service.EnqueueAsync(req);

        // Requester từ school-hn-01 cố tình liệt kê job của school-sg-02 -> Bị từ chối
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.ListJobsAsync(schoolSg, "teacher-hn", requesterScope: schoolHn);
        });
    }

    [TestMethod]
    public async Task Legacy_snapshot_is_migrated_and_preserves_idempotency_via_job_service()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobstore_legacy_migration_{Guid.NewGuid():N}.json");
        try
        {
            var legacyJobId = Guid.NewGuid();
            var rawScopeResourceId = "school-legacy-hn-01";
            var rawRequesterId = "teacher-legacy-1";
            var jobType = "SampleJob";
            var payloadRef = "s3://legacy-snapshot/sample.json";
            var idempotencyKey = "legacy-idem-123";
            var correlationId = "corr-legacy-1";

            // Tạo fingerprint theo thuật toán cũ của HEAD^ (chưa chuẩn hóa ResourceId bằng ToUpperInvariant)
            var rawOldFingerprintInput = $"{jobType}:{payloadRef}:{JobScopeKind.School}:{rawScopeResourceId}";
            var oldFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawOldFingerprintInput))).ToLowerInvariant();

            // Khóa IdempotencyIndex theo định dạng cũ của HEAD^ (chưa chuẩn hóa ResourceId)
            var oldIdempotencyKey = $"{JobScopeKind.School}:{rawScopeResourceId}:{rawRequesterId}:{jobType}:{idempotencyKey}";

            var legacyJson = JsonSerializer.Serialize(new
            {
                Jobs = new[]
                {
                    new
                    {
                        Id = legacyJobId,
                        Type = jobType,
                        Status = (int)JobStatus.Pending,
                        PayloadReference = payloadRef,
                        CreatedBy = rawRequesterId,
                        ScopeKind = (int)JobScopeKind.School,
                        ScopeResourceId = rawScopeResourceId,
                        IdempotencyKey = idempotencyKey,
                        RequestFingerprint = oldFingerprint,
                        CorrelationId = correlationId,
                        AttemptCount = 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    }
                },
                IdempotencyIndex = new Dictionary<string, Guid>
                {
                    [oldIdempotencyKey] = legacyJobId
                },
                Attempts = new Dictionary<Guid, object>()
            }, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(tempFile, legacyJson);

            // Nạp snapshot cũ vào FileJobStore hiện tại -> Kích hoạt migration tự động và lưu snapshot nâng cấp
            var store = new FileJobStore(tempFile);
            var service = new JobService(store);

            // Enqueue cùng yêu cầu thông qua JobService
            var sameRequest = JobRequest.Create(
                jobType,
                payloadRef,
                rawRequesterId,
                JobScope.Create(JobScopeKind.School, rawScopeResourceId),
                idempotencyKey,
                correlationId);

            var enqueuedRef = await service.EnqueueAsync(sameRequest);

            // Xác nhận JobId được giữ nguyên và không xảy ra JobConflictException
            Assert.AreEqual(legacyJobId, enqueuedRef.Value);

            // Xác nhận enqueue với chữ hoa cũng trả về cùng JobId mà không xung đột
            var upperRequest = JobRequest.Create(
                jobType,
                payloadRef,
                rawRequesterId,
                JobScope.Create(JobScopeKind.School, rawScopeResourceId.ToUpperInvariant()),
                idempotencyKey,
                correlationId);

            var upperEnqueuedRef = await service.EnqueueAsync(upperRequest);
            Assert.AreEqual(legacyJobId, upperEnqueuedRef.Value);

            // Kiểm tra tệp trên đĩa đã được nâng cấp lên SchemaVersion 2
            var upgradedJson = await File.ReadAllTextAsync(tempFile);
            using var doc = JsonDocument.Parse(upgradedJson);
            Assert.IsTrue(doc.RootElement.TryGetProperty("SchemaVersion", out var versionProp));
            Assert.AreEqual(FileJobStore.CurrentSchemaVersion, versionProp.GetInt32());

            var expectedUpgradedKey = $"{JobScopeKind.School}:{rawScopeResourceId.ToUpperInvariant()}:{rawRequesterId}:{jobType}:{idempotencyKey}";
            Assert.IsTrue(doc.RootElement.GetProperty("IdempotencyIndex").TryGetProperty(expectedUpgradedKey, out var indexedJobIdProp));
            Assert.AreEqual(legacyJobId, indexedJobIdProp.GetGuid());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public void Postgres_migration_script_enforces_case_insensitive_unique_idempotency_index_and_scope_index()
    {
        // Tìm đường dẫn file migration SQL 001_create_jobs_and_job_attempts.sql
        string? projectRoot = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AiExamBank.slnx")))
            {
                projectRoot = dir.FullName;
                break;
            }
        }
        Assert.IsNotNull(projectRoot, "Repository root containing AiExamBank.slnx was not found.");

        var sqlPath = Path.Combine(projectRoot, "src", "Persistence", "Migrations", "001_create_jobs_and_job_attempts.sql");
        Assert.IsTrue(File.Exists(sqlPath), $"Migration file not found at: {sqlPath}");

        var sqlContent = File.ReadAllText(sqlPath);

        // 1. Xác nhận sử dụng UPPER(scope_resource_id) cho Unique Index
        StringAssert.Contains(sqlContent, "UPPER(scope_resource_id)");
        StringAssert.Contains(sqlContent, "uq_jobs_idempotency_upper");

        // 2. Xác nhận idx_jobs_scope_status cũng sử dụng UPPER(scope_resource_id)
        StringAssert.Contains(sqlContent, "idx_jobs_scope_status ON jobs (scope_kind, UPPER(scope_resource_id), status, created_at DESC)");

        // 3. Xác nhận ràng buộc case-sensitive cũ đã được loại bỏ
        Assert.DoesNotContain("CONSTRAINT uq_jobs_idempotency UNIQUE", sqlContent);
    }
}
