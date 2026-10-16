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

        var schoolList = await service.ListJobsAsync(SchoolScope, "teacher-1");
        Assert.HasCount(2, schoolList);

        var status1 = await service.GetJobStatusAsync(jobRef1, "teacher-1");
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
        var validStatus = await service.GetJobStatusAsync(jobRef, "teacher-hn", requesterScopeId: "school-hn-01");
        Assert.IsNotNull(validStatus);

        // Truy cập chéo trường khác: Bị từ chối (403 Forbidden)
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await service.GetJobStatusAsync(jobRef, "teacher-sg", requesterScopeId: "school-sg-02");
        });
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
}
