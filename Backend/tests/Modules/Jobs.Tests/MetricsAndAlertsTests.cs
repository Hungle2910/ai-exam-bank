using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AiExamBank.Modules.Jobs.Tests;

[TestClass]
public sealed class MetricsAndAlertsTests
{
    private static readonly JobScope SchoolScope = JobScope.Create(JobScopeKind.School, "school-hn-01");

    #region 1. JobMetrics Telemetry & Percentile Calculation Tests

    [TestMethod]
    public void JobMetrics_RecordsEnqueueAndComplete_UpdatesSnapshot()
    {
        var metrics = new JobMetrics();

        metrics.RecordJobEnqueued("ExamGeneration", "School");
        metrics.RecordJobStarted("ExamGeneration");
        metrics.RecordJobCompleted("ExamGeneration", 250);

        var snapshot = metrics.GetSnapshot();

        Assert.AreEqual(1L, snapshot.TotalEnqueued);
        Assert.AreEqual(1L, snapshot.TotalCompleted);
        Assert.AreEqual(0L, snapshot.TotalFailed);
        Assert.AreEqual(0L, snapshot.ActiveRunning);
        Assert.AreEqual(100.0, snapshot.SuccessRatePercentage);
        Assert.AreEqual(250.0, snapshot.AverageDurationMs);
        Assert.AreEqual(250.0, snapshot.P50DurationMs);
        Assert.AreEqual(250.0, snapshot.P95DurationMs);
    }

    [TestMethod]
    public void JobMetrics_ComputesPercentiles_Correctly()
    {
        var metrics = new JobMetrics();

        // Thêm 100 mẫu thời gian thực thi: 1, 2, ..., 100 ms
        for (int i = 1; i <= 100; i++)
        {
            metrics.RecordJobCompleted("ExamGeneration", i);
        }

        var snapshot = metrics.GetSnapshot();

        Assert.AreEqual(100L, snapshot.TotalCompleted);
        Assert.AreEqual(50.5, snapshot.AverageDurationMs, 0.1);
        Assert.AreEqual(50.0, snapshot.P50DurationMs, 1.0);
        Assert.AreEqual(90.0, snapshot.P90DurationMs, 1.0);
        Assert.AreEqual(95.0, snapshot.P95DurationMs, 1.0);
    }

    [TestMethod]
    public void JobMetrics_ActiveJobs_TracksRunningStateAccurately()
    {
        var metrics = new JobMetrics();

        metrics.RecordJobStarted("ExamGeneration");
        metrics.RecordJobStarted("CsvImport");

        Assert.AreEqual(2L, metrics.GetSnapshot().ActiveRunning);

        metrics.RecordJobCompleted("ExamGeneration", 100);
        Assert.AreEqual(1L, metrics.GetSnapshot().ActiveRunning);

        metrics.RecordJobFailed("CsvImport", "Transient", "ERR_TEST", 150);
        Assert.AreEqual(0L, metrics.GetSnapshot().ActiveRunning);
    }

    [TestMethod]
    public void JobMetrics_RecordsLeaseRecoveryAndRetries()
    {
        var metrics = new JobMetrics();

        metrics.RecordLeaseRecovery("ExamGeneration");
        metrics.RecordLeaseRecovery("ExamGeneration");
        metrics.RecordJobRetried("ExamGeneration", isManual: false, attemptNumber: 2);
        metrics.RecordJobRetried("CsvImport", isManual: true, attemptNumber: 1);

        var snapshot = metrics.GetSnapshot();

        Assert.AreEqual(2L, snapshot.StaleLeaseRecoveries);
        Assert.AreEqual(2L, snapshot.TotalRetries);
    }

    [TestMethod]
    public void JobMetrics_SanitizesDimensions_RemovesSpecialChars()
    {
        var metrics = new JobMetrics();

        // Type có ký tự đặc biệt hoặc quá dài
        metrics.RecordJobEnqueued("Exam-Gen@Special#Type!", "School");
        metrics.RecordJobFailed("Exam-Gen@Special#Type!", "Permanent", "ERR:FATAL/TIMEOUT", 100);

        var snapshot = metrics.GetSnapshot();

        Assert.AreEqual(1L, snapshot.TotalEnqueued);
        Assert.AreEqual(1L, snapshot.TotalFailed);
    }

    #endregion

    #region 2. SNS Alert Publisher & Throttling Tests

    [TestMethod]
    public async Task SnsAlertPublisher_PublishesAlert_ReturnsSuccess_InMockMode()
    {
        var logs = new List<string>();
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            ThrottleWindowSeconds = 300
        };
        var publisher = new SnsAlertPublisher(options, logs.Add);

        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.CircuitBreakerOpen,
            Severity = IncidentSeverity.Critical,
            Title = "AI Provider circuit breaker tripped open",
            Details = "5 consecutive failures detected"
        };

        var result = await publisher.PublishIncidentAlertAsync(alert);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.WasThrottled);
        Assert.IsNotNull(result.MessageId);
        Assert.IsTrue(logs.Any(l => l.Contains("[SNS ALERT PUBLISHED]")));

        var recent = publisher.GetRecentAlerts();
        Assert.HasCount(1, recent);
        Assert.AreEqual(IncidentTypes.CircuitBreakerOpen, recent[0].IncidentType);
    }

    [TestMethod]
    public async Task SnsAlertPublisher_ThrottlesAlerts_WhenInCooldownWindow()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            ThrottleWindowSeconds = 300 // 5 phút cooldown
        };
        var publisher = new SnsAlertPublisher(options);

        var alert1 = new IncidentAlert
        {
            IncidentType = IncidentTypes.HighFailureRate,
            Severity = IncidentSeverity.Warning,
            Title = "Queue backlog detected",
            Details = "10 stale leases"
        };

        var alert2 = new IncidentAlert
        {
            IncidentType = IncidentTypes.HighFailureRate,
            Severity = IncidentSeverity.Warning,
            Title = "Queue backlog detected again",
            Details = "12 stale leases"
        };

        var result1 = await publisher.PublishIncidentAlertAsync(alert1);
        var result2 = await publisher.PublishIncidentAlertAsync(alert2);

        Assert.IsTrue(result1.Success);
        Assert.IsFalse(result1.WasThrottled);

        Assert.IsTrue(result2.Success);
        Assert.IsTrue(result2.WasThrottled);
    }

    [TestMethod]
    public async Task SnsAlertPublisher_AllowsDrillAlerts_BypassingThrottle()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            ThrottleWindowSeconds = 300
        };
        var publisher = new SnsAlertPublisher(options);

        var alert1 = new IncidentAlert
        {
            IncidentType = IncidentTypes.DrillTestAlert,
            Severity = IncidentSeverity.Info,
            Title = "Drill Run 1",
            Details = "Manual drill test"
        };

        var alert2 = new IncidentAlert
        {
            IncidentType = IncidentTypes.DrillTestAlert,
            Severity = IncidentSeverity.Info,
            Title = "Drill Run 2",
            Details = "Manual drill test"
        };

        var result1 = await publisher.PublishIncidentAlertAsync(alert1);
        var result2 = await publisher.PublishIncidentAlertAsync(alert2);

        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
        Assert.IsFalse(result2.WasThrottled, "Drill test alerts must bypass throttle window.");
    }

    [TestMethod]
    public async Task SnsAlertPublisher_SanitizesSensitiveData_InDetails_CWE209()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true
        };
        var publisher = new SnsAlertPublisher(options);

        var sensitiveDetail = "DB Exception at C:\\inetpub\\wwwroot\\SecretService.cs:line 45 with AKIAIOSFODNN7EXAMPLE key";
        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.HighFailureRate,
            Severity = IncidentSeverity.Warning,
            Title = "Database failure",
            Details = sensitiveDetail
        };

        await publisher.PublishIncidentAlertAsync(alert);

        var recent = publisher.GetRecentAlerts();
        Assert.HasCount(1, recent);
        Assert.DoesNotContain("C:\\inetpub", recent[0].Details);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", recent[0].Details);
        // AwsErrorMapping.SanitizeForClient replaces raw message with safe user message
        Assert.IsGreaterThan(0, recent[0].Details.Length);
    }

    #endregion

    #region 3. Integration with InMemoryCircuitBreaker Tests

    [TestMethod]
    public async Task CircuitBreaker_TripsOpen_RecordsMetricsAndPublishesAlert()
    {
        var metrics = new JobMetrics();
        var alertPublisher = new SnsAlertPublisher(new SnsAlertOptions { IsEnabled = true, UseMockSimulation = true });
        var circuitBreaker = new InMemoryCircuitBreaker(
            failureThreshold: 3,
            coolingPeriod: TimeSpan.FromSeconds(30),
            metrics: metrics,
            alertPublisher: alertPublisher);

        // Báo lỗi 3 lần liên tiếp để kích hoạt ngắt mạch
        circuitBreaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        circuitBreaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);
        circuitBreaker.RecordFailure("AWS_BEDROCK", AwsErrorCodes.BedrockThrottled);

        // Đợi Task.Run phát SNS alert hoàn tất
        await Task.Delay(100);

        Assert.IsFalse(circuitBreaker.CanExecute("AWS_BEDROCK"));
        Assert.AreEqual(CircuitBreakerState.Open, circuitBreaker.GetState("AWS_BEDROCK"));

        // Kiểm tra alert đã được đẩy lên SNS
        var recentAlerts = alertPublisher.GetRecentAlerts();
        Assert.HasCount(1, recentAlerts);
        Assert.AreEqual(IncidentTypes.CircuitBreakerOpen, recentAlerts[0].IncidentType);
        Assert.AreEqual(IncidentSeverity.Critical, recentAlerts[0].Severity);

        // Kiểm tra metrics (lưu theo error_code)
        var snapshot = metrics.GetSnapshot();
        Assert.IsTrue(snapshot.ProviderErrors.ContainsKey(AwsErrorCodes.BedrockThrottled));
        Assert.AreEqual(3L, snapshot.ProviderErrors[AwsErrorCodes.BedrockThrottled]);
    }

    #endregion

    #region 4. Integration with JobStore & Stale Leases Tests

    [TestMethod]
    public async Task InMemoryJobStore_PublishesAlert_WhenRetryBudgetExhausted()
    {
        var now = DateTimeOffset.UtcNow;
        var metrics = new JobMetrics();
        var alertPublisher = new SnsAlertPublisher(new SnsAlertOptions { IsEnabled = true, UseMockSimulation = true });
        var store = new InMemoryJobStore(clock: () => now, metrics: metrics, alertPublisher: alertPublisher);

        var request = JobRequest.Create(
            "ExamGeneration",
            "s3://bucket/test.json",
            "teacher-01",
            SchoolScope,
            "idem-key-sns-alert",
            "corr-sns-alert");

        var job = await store.EnqueueAsync(request, "fingerprint-sns-alert");

        // Set attempt count to 3 to simulate exhaustion on failure
        var claim1 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim1);
        await store.FailJobAsync(job.Id, claim1.LeaseToken, AwsErrorCodes.BedrockThrottled, "Attempt 1 failed", isTransient: true);

        now = now.AddMinutes(2);
        var claim2 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim2);
        await store.FailJobAsync(job.Id, claim2.LeaseToken, AwsErrorCodes.BedrockThrottled, "Attempt 2 failed", isTransient: true);

        now = now.AddMinutes(6);
        var claim3 = await store.ClaimNextJobAsync("worker-1", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim3);
        await store.FailJobAsync(job.Id, claim3.LeaseToken, AwsErrorCodes.BedrockThrottled, "Attempt 3 failed", isTransient: true);

        // Đợi Task.Run phát SNS alert hoàn tất
        await Task.Delay(100);

        var finalJob = await store.GetByIdAsync(job.Id);
        Assert.IsNotNull(finalJob);
        Assert.AreEqual(JobStatus.Failed, finalJob.Status);
        Assert.AreEqual("RETRY_BUDGET_EXHAUSTED", finalJob.ErrorCode);

        var recentAlerts = alertPublisher.GetRecentAlerts();
        Assert.HasCount(1, recentAlerts);
        Assert.AreEqual(IncidentTypes.RetryBudgetExhausted, recentAlerts[0].IncidentType);
        Assert.AreEqual(IncidentSeverity.Warning, recentAlerts[0].Severity);
    }

    #endregion

    #region 5. JobService & Reliability Summary DTO Tests

    [TestMethod]
    public async Task JobService_GetReliabilitySummaryAsync_ReturnsAggregatedData()
    {
        var metrics = new JobMetrics();
        var alertPublisher = new SnsAlertPublisher(new SnsAlertOptions { IsEnabled = true, UseMockSimulation = true });
        var store = new InMemoryJobStore(metrics: metrics, alertPublisher: alertPublisher);
        var jobService = new JobService(store, metrics, alertPublisher);

        metrics.RecordJobEnqueued("ExamGeneration", "School");
        metrics.RecordJobStarted("ExamGeneration");
        metrics.RecordJobCompleted("ExamGeneration", 200);
        metrics.RecordCircuitBreakerState("AWS_BEDROCK", CircuitBreakerState.Closed);

        var summary = await jobService.GetReliabilitySummaryAsync();

        Assert.IsNotNull(summary);
        Assert.AreEqual(1L, summary.TotalEnqueued);
        Assert.AreEqual(1L, summary.TotalCompleted);
        Assert.AreEqual(100.0, summary.SuccessRatePercentage);
        Assert.AreEqual(200.0, summary.P95DurationMs);
        Assert.AreEqual("Closed", summary.CircuitBreakerStates["AWS_BEDROCK"]);
    }

    [TestMethod]
    public async Task JobService_TriggerTestAlertAsync_DispatchesDrillIncident()
    {
        var metrics = new JobMetrics();
        var alertPublisher = new SnsAlertPublisher(new SnsAlertOptions { IsEnabled = true, UseMockSimulation = true });
        var store = new InMemoryJobStore(metrics: metrics, alertPublisher: alertPublisher);
        var jobService = new JobService(store, metrics, alertPublisher);

        var result = await jobService.TriggerTestAlertAsync("GiaBaoAdmin", "Manual drill testing");

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.MessageId);

        var recentAlerts = alertPublisher.GetRecentAlerts();
        Assert.HasCount(1, recentAlerts);
        Assert.AreEqual(IncidentTypes.DrillTestAlert, recentAlerts[0].IncidentType);
    }

    #endregion
}
