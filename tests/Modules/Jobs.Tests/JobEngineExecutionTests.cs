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
    [DataRow(false)]
    [DataRow(true)]
    public async Task Successful_retry_clears_job_errors_and_preserves_attempt_history(bool useFileStore)
    {
        var now = DateTimeOffset.UtcNow;
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobs-retry-{Guid.NewGuid():N}.json");
        using var fileStore = useFileStore ? new FileJobStore(tempFile, () => now) : null;
        IJobStore store = fileStore is not null ? fileStore : new InMemoryJobStore(() => now);
        try
        {
            var service = new JobService(store);
            var request = JobRequest.Create("SampleJob", "payload", "user-1", SchoolScope, "retry-success", "corr-retry");
            var job = await service.EnqueueAsync(request);
            var first = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(first);
            Assert.IsTrue(await store.FailJobAsync(job.Value, first.LeaseToken, "TIMEOUT", "Please retry.", isTransient: true));
            var failed = await service.GetJobStatusAsync(job, "user-1", SchoolScope);
            Assert.AreEqual("TIMEOUT", failed.ErrorCode);
            Assert.AreEqual("Please retry.", failed.SafeMessage);

            now = now.AddMinutes(1);
            var retry = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(retry);
            Assert.IsTrue(await store.CompleteJobAsync(job.Value, retry.LeaseToken, "result-1"));

            // A fresh file-store instance verifies that clearing errors was persisted.
            using var reopened = useFileStore ? new FileJobStore(tempFile, () => now) : null;
            var reader = reopened is not null ? new JobService(reopened) : service;
            var completed = await reader.GetJobStatusAsync(job, "user-1", SchoolScope);
            Assert.AreEqual(JobStatus.Completed, completed.Status);
            Assert.AreEqual(100, completed.ProgressPercentage);
            Assert.AreEqual("result-1", completed.ResultId);
            Assert.IsNull(completed.ErrorCode);
            Assert.IsNull(completed.SafeMessage);

            var attempts = reopened is not null
                ? reopened.GetAttempts(job.Value)
                : ((InMemoryJobStore)store).GetAttempts(job.Value);
            Assert.HasCount(2, attempts);
            Assert.AreEqual(AttemptStatus.Failed, attempts[0].Status);
            Assert.AreEqual("TIMEOUT", attempts[0].ErrorCode);
            Assert.AreEqual(AttemptStatus.Completed, attempts[1].Status);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    [DataRow("ExamGeneration")]
    [DataRow("CsvImport")]
    public async Task Job_status_and_list_preserve_job_type(string jobType)
    {
        var service = new JobService(new InMemoryJobStore());
        var request = JobRequest.Create(jobType, "payload", "user-1", SchoolScope, "type-test", "corr-type");
        var job = await service.EnqueueAsync(request);

        var status = await service.GetJobStatusAsync(job, "user-1", SchoolScope);
        var list = await service.ListJobsAsync(SchoolScope, "user-1", SchoolScope);

        Assert.AreEqual(jobType, status.JobType);
        Assert.HasCount(1, list);
        Assert.AreEqual(jobType, list[0].JobType);
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
    public async Task Commit_with_expired_lease_succeeds_before_recovery_and_fences_after_recovery()
    {
        var currentTime = DateTimeOffset.UtcNow;
        var store = new InMemoryJobStore(() => currentTime);

        var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-lease-1", "corr-lease");
        var job = await store.EnqueueAsync(request, "fp-lease");

        var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        // Giả lập thời gian trôi qua 3 phút (> 2 phút lease), nhưng RecoverExpiredLeasesAsync CHƯA CHẠY
        currentTime = currentTime.AddMinutes(3);

        // Worker commit kết quả hoặc cập nhật tiến độ trước khi recovery chạy vẫn thành công để tránh lãng phí công sức hợp lệ
        var progressUpdated = await store.UpdateProgressAsync(job.Id, claim.LeaseToken, 50);
        Assert.IsTrue(progressUpdated, "UpdateProgress with expired lease before recovery should succeed.");

        var heartbeatSucceeded = await store.HeartbeatAsync(job.Id, claim.LeaseToken, TimeSpan.FromMinutes(2));
        Assert.IsTrue(heartbeatSucceeded, "Heartbeat with expired lease before recovery should succeed.");

        var completed = await store.CompleteJobAsync(job.Id, claim.LeaseToken, "res-1");
        Assert.IsTrue(completed, "Complete with expired lease before recovery should succeed.");

        // Tạo job thứ hai để kiểm tra khi Recovery chạy trước
        var request2 = JobRequest.Create("ExamGen", "s3://ref-2", "teacher-hn", SchoolScope, "idem-lease-2", "corr-lease-2");
        var job2 = await store.EnqueueAsync(request2, "fp-lease-2");
        var claim2 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim2);

        // Quá hạn lease và Recovery chạy trước
        currentTime = currentTime.AddMinutes(3);
        var recovered = await store.RecoverExpiredLeasesAsync();
        Assert.AreEqual(1, recovered);

        // Worker cũ cố commit sau khi recovery đã chạy -> Bị Fencing chặn đứng do Attempt đã bị đánh dấu Abandoned
        var progressAfterRecovery = await store.UpdateProgressAsync(job2.Id, claim2.LeaseToken, 50);
        Assert.IsFalse(progressAfterRecovery, "UpdateProgress after recovery must fail.");

        var heartbeatAfterRecovery = await store.HeartbeatAsync(job2.Id, claim2.LeaseToken, TimeSpan.FromMinutes(2));
        Assert.IsFalse(heartbeatAfterRecovery, "Heartbeat after recovery must fail.");

        var completeAfterRecovery = await store.CompleteJobAsync(job2.Id, claim2.LeaseToken, "res-2");
        Assert.IsFalse(completeAfterRecovery, "Complete after recovery must fail.");

        var failAfterRecovery = await store.FailJobAsync(job2.Id, claim2.LeaseToken, "ERR", "Err", false);
        Assert.IsFalse(failAfterRecovery, "Fail after recovery must fail.");
    }

    [TestMethod]
    public async Task FileJobStore_commit_with_expired_lease_succeeds_before_recovery_and_fences_after_recovery()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobstore_lease_fence_{Guid.NewGuid():N}.json");
        try
        {
            var currentTime = DateTimeOffset.UtcNow;
            using var store = new FileJobStore(tempFile, () => currentTime);

            var request = JobRequest.Create("ExamGen", "s3://ref", "teacher-hn", SchoolScope, "idem-file-lease-1", "corr-file-lease");
            var job = await store.EnqueueAsync(request, "fp-file-lease");

            var claim = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim);

            // Giả lập thời gian trôi qua 3 phút (> 2 phút lease), nhưng RecoverExpiredLeasesAsync chưa chạy
            currentTime = currentTime.AddMinutes(3);

            var completed = await store.CompleteJobAsync(job.Id, claim.LeaseToken, "res-file-1");
            Assert.IsTrue(completed, "FileJobStore complete with expired lease before recovery should succeed.");

            // Job thứ hai kiểm tra sau khi recovery chạy
            var request2 = JobRequest.Create("ExamGen", "s3://ref-2", "teacher-hn", SchoolScope, "idem-file-lease-2", "corr-file-lease-2");
            var job2 = await store.EnqueueAsync(request2, "fp-file-lease-2");
            var claim2 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim2);

            currentTime = currentTime.AddMinutes(3);
            var recovered = await store.RecoverExpiredLeasesAsync();
            Assert.AreEqual(1, recovered);

            var completeAfterRecovery = await store.CompleteJobAsync(job2.Id, claim2.LeaseToken, "res-file-2");
            Assert.IsFalse(completeAfterRecovery, "FileJobStore complete after recovery must fail.");
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

    [TestMethod]
    public async Task Legacy_handler_overload_is_invoked_and_returns_success_through_default_interface_implementation()
    {
        var legacyHandler = new LegacySampleJobHandler();
        IJobHandler handler = legacyHandler;

        var request = JobRequest.Create("LegacyJob", "s3://payload", "user-1", SchoolScope, "idem-1", "corr-1");
        var context = new JobExecutionContext
        {
            JobId = JobRef.Create(Guid.NewGuid()),
            Request = request,
            AttemptNumber = 1
        };

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(legacyHandler.WasExecuted);
    }

    [TestMethod]
    public async Task Worker_commits_job_as_completed_even_when_host_token_is_cancelled_immediately_after_handler_success()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        using var hostCts = new CancellationTokenSource();

        var cancelHandler = new CancellingJobHandler(hostCts);
        registry.Register(cancelHandler);

        var request = JobRequest.Create("CancellingJob", "s3://payload", "user-1", SchoolScope, "idem-cancel", "corr-cancel");
        var enqueuedJob = await store.EnqueueAsync(request, "fp-cancel");

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker-cancel",
            PollingInterval = TimeSpan.FromMilliseconds(20),
            LeaseDuration = TimeSpan.FromSeconds(10)
        });

        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);

        // Start worker background loop
        _ = worker.StartAsync(hostCts.Token);

        // Wait for handler to cancel hostCts and CompleteJobAsync to finish
        var completed = false;
        for (var i = 0; i < 40; i++)
        {
            var job = await store.GetByIdAsync(enqueuedJob.Id);
            if (job?.Status == JobStatus.Completed)
            {
                completed = true;
                break;
            }
            await Task.Delay(50);
        }

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch
        {
            // Worker shutdown might throw on cancellation, ignore
        }

        Assert.IsTrue(completed, "Job should reach Completed status even though hostStoppingToken was cancelled upon handler completion.");
        var finalJob = await store.GetByIdAsync(enqueuedJob.Id);
        Assert.IsNotNull(finalJob);
        Assert.AreEqual(JobStatus.Completed, finalJob.Status);
        Assert.AreEqual("cancel-result", finalJob.ResultId);
    }

    /// <summary>
    /// Kiểm tra FileJobStore khi claim trúng lease đã hết hạn thì phải áp dụng backoff cooldown,
    /// đưa job về Pending và không cho claim ngay lập tức trước khi hết thời gian chờ.
    /// </summary>
    [TestMethod]
    public async Task FileJobStore_claim_on_expired_lease_applies_backoff_cooldown()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"jobstore-backoff-{Guid.NewGuid():N}.json");
        try
        {
            var now = DateTimeOffset.UtcNow;
            using var store = new FileJobStore(tempFile, () => now);

            var req = JobRequest.Create("ExamGen", "s3://ref", "user-1", SchoolScope, "key-backoff", "corr-backoff");
            var job = await store.EnqueueAsync(req, "fp-backoff");

            // Lượt 1: Worker-1 claim với lease 2 phút
            var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim1);
            Assert.AreEqual(1, claim1.Attempt.AttemptNumber);

            // Tua thời gian quá hạn lease 2 phút (Worker-1 bị crash)
            now = now.AddMinutes(2).AddSeconds(5);

            // Worker-2 gọi ClaimNextJobAsync:
            // Phải đánh dấu attempt cũ là Abandoned, đưa job về Pending với backoff 1 phút và TRẢ VỀ NULL (chưa claim ngay)
            var claim2Premature = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
            Assert.IsNull(claim2Premature, "Job must not be immediately claimed; it must enter cooldown.");

            var jobAfterAbandon = await store.GetByIdAsync(job.Id);
            Assert.IsNotNull(jobAfterAbandon);
            Assert.AreEqual(JobStatus.Pending, jobAfterAbandon.Status);
            Assert.IsNotNull(jobAfterAbandon.AvailableAt);
            Assert.IsTrue(jobAfterAbandon.AvailableAt > now);

            // Thử claim lại khi chưa hết 1 phút cooldown: vẫn phải trả về null
            now = now.AddSeconds(30);
            var claimStillCooling = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
            Assert.IsNull(claimStillCooling, "Job must remain unclaimable before cooldown expires.");

            // Tua qua 1 phút cooldown: Worker-2 claim thành công attempt 2
            now = now.AddSeconds(35);
            var claim2Ready = await store.ClaimNextJobAsync("worker-2", TimeSpan.FromMinutes(2));
            Assert.IsNotNull(claim2Ready, "Job must be claimable after cooldown expires.");
            Assert.AreEqual(2, claim2Ready.Attempt.AttemptNumber);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed class LegacySampleJobHandler : IJobHandler
    {
        public string JobType => "LegacyJob";
        public bool WasExecuted { get; private set; }

        public Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingJobHandler : IJobHandler
    {
        private readonly CancellationTokenSource _cts;
        public string JobType => "CancellingJob";

        public CancellingJobHandler(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            // Simulate host stopping token cancelled immediately when handler finishes
            _cts.Cancel();
            return Task.FromResult(JobExecutionResult.Success("cancel-result"));
        }
    }

    private sealed class EmptyJobHandler : IJobHandler
    {
        public string JobType => "EmptyJob";
    }

    private sealed class ChildTimeoutJobHandler : IJobHandler
    {
        public string JobType => "ChildTimeoutJob";

        public Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("Internal HTTP timeout");
        }
    }

    /// <summary>
    /// Kiểm tra Handler không override bất kỳ overload ExecuteAsync nào phải ném NotImplementedException
    /// và ghi nhận lỗi hệ thống, không được báo hoàn thành giả.
    /// </summary>
    [TestMethod]
    public async Task Handler_without_overrides_fails_and_does_not_report_completed()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        registry.Register(new EmptyJobHandler());

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(5)
        });

        var request = JobRequest.Create("EmptyJob", "s3://payload", "tester", SchoolScope, "key-empty", "corr-empty");
        var job = await store.EnqueueAsync(request, "fp-empty");

        using var workerCts = new CancellationTokenSource();
        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(workerCts.Token);

        BackgroundJob? updatedJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            var current = await store.GetByIdAsync(job.Id);
            if (current != null && current.Status != JobStatus.Running && current.AttemptCount >= 1 && current.ErrorCode != null)
            {
                updatedJob = current;
                break;
            }
        }

        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.IsNotNull(updatedJob);
        Assert.AreNotEqual(JobStatus.Completed, updatedJob.Status, "Handler without overrides must not report Completed.");
        Assert.AreEqual(JobStatus.Pending, updatedJob.Status, "Transient unhandled exception schedules retry.");
        Assert.AreEqual("UNHANDLED_EXCEPTION", updatedJob.ErrorCode);
    }

    /// <summary>
    /// Kiểm tra khi Handler ném TaskCanceledException hoặc OperationCanceledException
    /// nhưng CancellationToken của Job chưa bị hủy thì phải coi là lỗi ngoại lệ (transient failure),
    /// đưa job về Pending để retry thay vì xem là người dùng bấm hủy.
    /// </summary>
    [TestMethod]
    public async Task TaskCanceledException_when_job_token_not_cancelled_fails_as_transient()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        registry.Register(new ChildTimeoutJobHandler());

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(5)
        });

        var request = JobRequest.Create("ChildTimeoutJob", "s3://payload", "tester", SchoolScope, "key-timeout", "corr-timeout");
        var job = await store.EnqueueAsync(request, "fp-timeout");

        using var workerCts = new CancellationTokenSource();
        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(workerCts.Token);

        BackgroundJob? updatedJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            var current = await store.GetByIdAsync(job.Id);
            if (current != null && current.Status != JobStatus.Running && current.AttemptCount >= 1 && current.ErrorCode != null)
            {
                updatedJob = current;
                break;
            }
        }

        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        Assert.IsNotNull(updatedJob);
        Assert.AreEqual(JobStatus.Pending, updatedJob.Status, "Child cancellation when job token is active must schedule retry.");
        Assert.AreEqual("UNHANDLED_EXCEPTION", updatedJob.ErrorCode);
    }

    private sealed class FailingCancellingJobHandler : IJobHandler
    {
        private readonly CancellationTokenSource _cts;
        public string JobType => "FailingCancellingJob";

        public FailingCancellingJobHandler(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            _cts.Cancel();
            return Task.FromResult(JobExecutionResult.Failure("ROW_VALIDATION_ERROR", "Data invalid", isTransient: false));
        }
    }

    /// <summary>
    /// Kiểm tra khi Handler trả Failure(isTransient: false) và token dừng host bị hủy,
    /// Worker vẫn ghi nhận trạng thái Failed với đúng ErrorCode nhờ token riêng có timeout.
    /// </summary>
    [TestMethod]
    public async Task Worker_fails_job_as_permanent_failure_even_when_host_token_is_cancelled()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        using var hostCts = new CancellationTokenSource();

        var handler = new FailingCancellingJobHandler(hostCts);
        registry.Register(handler);

        var request = JobRequest.Create("FailingCancellingJob", "s3://payload", "user-1", SchoolScope, "idem-fail-cancel", "corr-fail-cancel");
        var enqueuedJob = await store.EnqueueAsync(request, "fp-fail-cancel");

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker-fail-cancel",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(5)
        });

        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(hostCts.Token);

        BackgroundJob? finalJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            var current = await store.GetByIdAsync(enqueuedJob.Id);
            if (current != null && current.Status == JobStatus.Failed)
            {
                finalJob = current;
                break;
            }
        }

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        worker.Dispose();

        Assert.IsNotNull(finalJob);
        Assert.AreEqual(JobStatus.Failed, finalJob.Status);
        Assert.AreEqual("ROW_VALIDATION_ERROR", finalJob.ErrorCode);
    }

    /// <summary>
    /// Kiểm tra CancelJobAsync ném KeyNotFoundException khi không tìm thấy Job ID.
    /// </summary>
    [TestMethod]
    public async Task CancelJobAsync_throws_key_not_found_exception_when_job_does_not_exist()
    {
        var store = new InMemoryJobStore();
        var service = new JobService(store);
        var nonExistentRef = JobRef.Create(Guid.NewGuid());

        await Assert.ThrowsExactlyAsync<KeyNotFoundException>(async () =>
        {
            await service.CancelJobAsync(nonExistentRef, "tester", SchoolScope);
        });
    }

    /// <summary>
    /// Kiểm tra WorkerId mặc định giữ nguyên GUID duy nhất ngay cả khi tên máy dài.
    /// </summary>
    [TestMethod]
    public void Worker_id_is_unique_and_preserves_guid_even_with_long_machine_name()
    {
        var opt1 = new JobWorkerOptions();
        var opt2 = new JobWorkerOptions();

        Assert.AreNotEqual(opt1.WorkerId, opt2.WorkerId);
        Assert.StartsWith("worker-", opt1.WorkerId);
        var parts = opt1.WorkerId.Split('-');
        var lastPart = parts[^1];
        Assert.AreEqual(32, lastPart.Length, "Phần cuối của WorkerId phải là GUID dạng N dài 32 ký tự.");
    }

    private sealed class ObservingLongRunningJobHandler : IJobHandler
    {
        public string JobType => "SilentLongRunningJob";
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            using var _ = cancellationToken.Register(() => CancellationObserved.TrySetResult());
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            return JobExecutionResult.Success("done");
        }
    }

    /// <summary>
    /// Kiểm tra Cancellation Watcher phát hiện tín hiệu hủy và ngắt Handler ngay cả khi Handler không gọi ReportProgressAsync.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_watcher_cancels_running_job_when_handler_does_not_report_progress()
    {
        var store = new InMemoryJobStore();
        var registry = new JobRegistry();
        var handler = new ObservingLongRunningJobHandler();
        registry.Register(handler);

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker-watcher",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromSeconds(30),
            LeaseDuration = TimeSpan.FromSeconds(60)
        });

        var request = JobRequest.Create("SilentLongRunningJob", "s3://payload", "user-1", SchoolScope, "idem-watcher", "corr-watcher");
        var job = await store.EnqueueAsync(request, "fp-watcher");

        using var hostCts = new CancellationTokenSource();
        var worker = new JobWorkerService(store, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(hostCts.Token);

        // Chờ job chuyển sang Running
        BackgroundJob? runningJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            var current = await store.GetByIdAsync(job.Id);
            if (current != null && current.Status == JobStatus.Running)
            {
                runningJob = current;
                break;
            }
        }
        Assert.IsNotNull(runningJob, "Job phải được claim và chuyển sang Running.");

        // Kích hoạt hủy trên Store
        var cancelSuccess = await store.RequestCancellationAsync(job.Id, "user-1");
        Assert.IsTrue(cancelSuccess);

        // Chờ handler thực sự nhận được tín hiệu hủy thông qua CancellationToken do Watcher kích hoạt
        var observed = await Task.WhenAny(handler.CancellationObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        var cancelled = observed == handler.CancellationObserved.Task;

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        worker.Dispose();

        Assert.IsTrue(cancelled, "Handler phải nhận tín hiệu hủy qua CancellationToken do Watcher kích hoạt trong vòng vài giây mà không cần đợi heartbeat 30s.");
    }

    private sealed class FaultyGetByIdJobStore : IJobStore
    {
        private readonly IJobStore _inner;
        private int _faultInjected;

        public bool FaultInjected => Volatile.Read(ref _faultInjected) == 1;

        public FaultyGetByIdJobStore(IJobStore inner)
        {
            _inner = inner;
        }

        public async Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _faultInjected, 1) == 0)
            {
                throw new InvalidOperationException("Simulated transient read failure in watcher loop.");
            }
            return await _inner.GetByIdAsync(jobId, cancellationToken);
        }

        public Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default)
            => _inner.EnqueueAsync(request, requestFingerprint, cancellationToken);

        public Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
            => _inner.ListByScopeAsync(scope, statusFilter, cancellationToken);

        public Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            => _inner.ClaimNextJobAsync(workerId, leaseDuration, cancellationToken);

        public Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            => _inner.HeartbeatAsync(jobId, leaseToken, leaseDuration, cancellationToken);

        public Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default)
            => _inner.UpdateProgressAsync(jobId, leaseToken, progressPercentage, cancellationToken);

        public Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default)
            => _inner.CompleteJobAsync(jobId, leaseToken, resultId, cancellationToken);

        public Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default)
            => _inner.FailJobAsync(jobId, leaseToken, errorCode, safeErrorMessage, isTransient, cancellationToken);

        public Task<bool> RequestCancellationAsync(Guid jobId, string requesterId, CancellationToken cancellationToken = default)
            => _inner.RequestCancellationAsync(jobId, requesterId, cancellationToken);

        public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
            => _inner.RecoverExpiredLeasesAsync(cancellationToken);
    }

    /// <summary>
    /// Kiểm tra Watcher không bị chết khi gặp lỗi đọc tạm thời (transient error) từ GetByIdAsync,
    /// mà vẫn kiên trì thử lại ở tick kế tiếp và truyền tín hiệu hủy đến handler thành công.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_watcher_survives_transient_read_fault_and_propagates_cancellation()
    {
        var innerStore = new InMemoryJobStore();
        var faultyStore = new FaultyGetByIdJobStore(innerStore);
        var registry = new JobRegistry();
        var handler = new ObservingLongRunningJobHandler();
        registry.Register(handler);

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker-watcher-fault",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromSeconds(30),
            LeaseDuration = TimeSpan.FromSeconds(60)
        });

        var request = JobRequest.Create("SilentLongRunningJob", "s3://payload", "user-1", SchoolScope, "idem-watcher-fault", "corr-watcher-fault");
        var job = await innerStore.EnqueueAsync(request, "fp-watcher-fault");

        using var hostCts = new CancellationTokenSource();
        var worker = new JobWorkerService(faultyStore, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(hostCts.Token);

        // Chờ job chuyển sang Running
        BackgroundJob? runningJob = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(50);
            var current = await innerStore.GetByIdAsync(job.Id);
            if (current != null && current.Status == JobStatus.Running)
            {
                runningJob = current;
                break;
            }
        }
        Assert.IsNotNull(runningJob, "Job phải được claim và chuyển sang Running.");

        // Kích hoạt hủy trên Store
        var cancelSuccess = await innerStore.RequestCancellationAsync(job.Id, "user-1");
        Assert.IsTrue(cancelSuccess);

        // Chờ handler nhận được hủy qua TaskCompletionSource do watcher truyền tới (sau khi watcher đã vượt qua lỗi đọc tạm thời ở tick đầu)
        var observed = await Task.WhenAny(handler.CancellationObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        var cancelled = observed == handler.CancellationObserved.Task;

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        worker.Dispose();

        Assert.IsTrue(faultyStore.FaultInjected, "Lỗi đọc giả lập phải xảy ra.");
        Assert.IsTrue(cancelled, "Handler phải nhận tín hiệu hủy sau khi watcher vượt qua lỗi đọc tạm thời ban đầu.");
    }

    private sealed class DelayedCompletionJobStore : IJobStore
    {
        private readonly IJobStore _inner;
        private readonly TimeSpan _completionDelay;

        public DelayedCompletionJobStore(IJobStore inner, TimeSpan completionDelay)
        {
            _inner = inner;
            _completionDelay = completionDelay;
        }

        public async Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default)
        {
            // Mô phỏng tranh chấp lock/mutex giữ quyền ghi khiến CompleteJobAsync bị hoãn lại
            await Task.Delay(_completionDelay, cancellationToken);
            return await _inner.CompleteJobAsync(jobId, leaseToken, resultId, cancellationToken);
        }

        public Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default)
            => _inner.EnqueueAsync(request, requestFingerprint, cancellationToken);

        public Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(jobId, cancellationToken);

        public Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
            => _inner.ListByScopeAsync(scope, statusFilter, cancellationToken);

        public Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            => _inner.ClaimNextJobAsync(workerId, leaseDuration, cancellationToken);

        public Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            => _inner.HeartbeatAsync(jobId, leaseToken, leaseDuration, cancellationToken);

        public Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default)
            => _inner.UpdateProgressAsync(jobId, leaseToken, progressPercentage, cancellationToken);

        public Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default)
            => _inner.FailJobAsync(jobId, leaseToken, errorCode, safeErrorMessage, isTransient, cancellationToken);

        public Task<bool> RequestCancellationAsync(Guid jobId, string requesterId, CancellationToken cancellationToken = default)
            => _inner.RequestCancellationAsync(jobId, requesterId, cancellationToken);

        public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
            => _inner.RecoverExpiredLeasesAsync(cancellationToken);
    }

    private sealed class FastJobHandler : IJobHandler
    {
        public string JobType => "FastJob";

        public Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobExecutionResult.Success("fast-result"));
        }
    }

    /// <summary>
    /// Kiểm tra heartbeat vẫn duy trì hoạt động trong quá trình ghi terminal state (CompleteJobAsync)
    /// khi có tranh chấp mutex/lock làm chậm quá trình ghi, ngăn việc recovery thu hồi nhầm lease và không để job bị chạy lại.
    /// </summary>
    [TestMethod]
    public async Task Heartbeat_kept_alive_during_terminal_write_prevents_lease_expiration_and_duplicate_execution()
    {
        var innerStore = new InMemoryJobStore();
        // Trì hoãn CompleteJobAsync 2.5s (lớn hơn LeaseDuration 2s)
        var delayedStore = new DelayedCompletionJobStore(innerStore, TimeSpan.FromSeconds(2.5));
        var registry = new JobRegistry();
        registry.Register(new FastJobHandler());

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "test-worker-contention",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            MaxPollingInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromSeconds(1),
            LeaseDuration = TimeSpan.FromSeconds(2),
            RecoveryInterval = TimeSpan.FromSeconds(60)
        });

        var request = JobRequest.Create("FastJob", "s3://payload", "user-1", SchoolScope, "idem-contention", "corr-contention");
        var job = await innerStore.EnqueueAsync(request, "fp-contention");

        using var hostCts = new CancellationTokenSource();
        var worker = new JobWorkerService(delayedStore, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(hostCts.Token);

        // Đợi 2.2s (tổng 2.2s > LeaseDuration 2s ban đầu, lúc này handler đã xong nhưng CompleteJobAsync vẫn đang bị hoãn do contention)
        await Task.Delay(TimeSpan.FromMilliseconds(2200));

        // Kích hoạt quét thu hồi (nếu heartbeat bị dừng trước CompleteJobAsync thì lease đã hết hạn và bị thu hồi!)
        var recovered = await innerStore.RecoverExpiredLeasesAsync();
        Assert.AreEqual(0, recovered, "Lease không được bị thu hồi vì heartbeat vẫn đang gia hạn ngầm trong lúc CompleteJobAsync đang chờ ghi nhận.");

        // Chờ CompleteJobAsync hoàn tất (tổng delay 2.5s)
        await Task.Delay(TimeSpan.FromSeconds(1));

        var completedJob = await innerStore.GetByIdAsync(job.Id);
        Assert.IsNotNull(completedJob);
        Assert.AreEqual(JobStatus.Completed, completedJob.Status);
        Assert.AreEqual(1, completedJob.AttemptCount, "Job chỉ được chạy đúng 1 lần duy nhất, không bị re-run do tranh chấp mutex.");

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        worker.Dispose();
    }

    [TestMethod]
    public async Task Worker_does_not_fail_job_as_unhandled_exception_when_complete_step_throws_exception()
    {
        var innerStore = new InMemoryJobStore();
        var throwingStore = new ThrowingCompleteJobStore(innerStore);
        var registry = new JobRegistry();
        int handlerExecutionCount = 0;
        registry.Register(new CountingSuccessfulJobHandler(() => Interlocked.Increment(ref handlerExecutionCount)));

        var options = Options.Create(new JobWorkerOptions
        {
            WorkerId = "throwing-complete-worker",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            LeaseDuration = TimeSpan.FromSeconds(5)
        });

        var request = JobRequest.Create("CountingSuccessfulJob", "s3://payload", "tester", SchoolScope, "key-throw-complete", "corr-throw-complete");
        var job = await innerStore.EnqueueAsync(request, "fp-throw-complete");

        using var hostCts = new CancellationTokenSource();
        var worker = new JobWorkerService(throwingStore, registry, options, NullLogger<JobWorkerService>.Instance);
        await worker.StartAsync(hostCts.Token);

        // Chờ worker claim và thực thi handler thông qua vòng thăm dò có timeout thay vì fixed delay
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (Volatile.Read(ref handlerExecutionCount) < 1 && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(25);
        }

        // Chờ thêm một khoảng ngắn để worker xử lý xong khối catch từ CompleteJobAsync
        await Task.Delay(100);

        var currentJob = await innerStore.GetByIdAsync(job.Id);
        Assert.IsNotNull(currentJob);
        // Handler đã chạy thành công 1 lần và job đã được worker claim với đúng 1 lượt chạy
        Assert.AreEqual(1, Volatile.Read(ref handlerExecutionCount), "Handler phải được thực thi đúng 1 lần.");
        Assert.AreEqual(1, currentJob.AttemptCount, "Job phải được worker claim và ghi nhận đúng 1 lượt chạy.");
        // Job không bị đưa về Pending với UNHANDLED_EXCEPTION
        Assert.AreNotEqual("UNHANDLED_EXCEPTION", currentJob.ErrorCode, "Lỗi commit CompleteJobAsync không được coi là UNHANDLED_EXCEPTION của handler.");

        try
        {
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        worker.Dispose();
    }

    private sealed class ThrowingCompleteJobStore : IJobStore
    {
        private readonly IJobStore _inner;

        public ThrowingCompleteJobStore(IJobStore inner) => _inner = inner;

        public Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default) =>
            _inner.EnqueueAsync(request, requestFingerprint, cancellationToken);

        public Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            _inner.ClaimNextJobAsync(workerId, leaseDuration, cancellationToken);

        public Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            _inner.HeartbeatAsync(jobId, leaseToken, leaseDuration, cancellationToken);

        public Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default) =>
            _inner.UpdateProgressAsync(jobId, leaseToken, progressPercentage, cancellationToken);

        public Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("Simulated mutex lock timeout during CompleteJobAsync.");
        }

        public Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default) =>
            _inner.FailJobAsync(jobId, leaseToken, errorCode, safeErrorMessage, isTransient, cancellationToken);

        public Task<bool> RequestCancellationAsync(Guid jobId, string requestedBy, CancellationToken cancellationToken = default) =>
            _inner.RequestCancellationAsync(jobId, requestedBy, cancellationToken);

        public Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(jobId, cancellationToken);

        public Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default) =>
            _inner.ListByScopeAsync(scope, statusFilter, cancellationToken);

        public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default) =>
            _inner.RecoverExpiredLeasesAsync(cancellationToken);
    }

    private sealed class CountingSuccessfulJobHandler : IJobHandler
    {
        private readonly Action _onExecute;
        public string JobType => "CountingSuccessfulJob";

        public CountingSuccessfulJobHandler(Action onExecute) => _onExecute = onExecute;

        public Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            _onExecute();
            return Task.FromResult(JobExecutionResult.Success("res-success"));
        }
    }
}
