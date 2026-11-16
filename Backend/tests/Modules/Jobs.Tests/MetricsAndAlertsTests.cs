using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
        Assert.IsTrue(logs.Any(l => l.Contains("[SNS ALERT SIMULATED]") || l.Contains("[SNS ALERT PUBLISHED]")));

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
    public async Task SnsAlertPublisher_AppliesDrillThrottling_ToPreventSpam()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            DrillThrottleWindowSeconds = 60
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
        Assert.IsFalse(result1.WasThrottled);

        // Lần thứ 2 gọi ngay lập tức phải bị áp dụng rate limiting
        Assert.IsTrue(result2.Success);
        Assert.IsTrue(result2.WasThrottled, "Drill test alerts must apply drill rate limiting to prevent spam.");
        StringAssert.Contains(result2.Error, "giới hạn tốc độ");
    }

    [TestMethod]
    public async Task SnsAlertPublisher_LiveMode_FailsWhenTopicArnMissing()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = false, // Live Mode
            TopicArn = null
        };
        var publisher = new SnsAlertPublisher(options);

        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.CircuitBreakerOpen,
            Severity = IncidentSeverity.Critical,
            Title = "Live Alert",
            Details = "Live failure"
        };

        var result = await publisher.PublishIncidentAlertAsync(alert);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "TopicArn is required");
    }

    [TestMethod]
    public async Task SnsAlertPublisher_LiveMode_FailsWhenAwsClientMissing()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = false, // Live Mode
            TopicArn = "arn:aws:sns:ap-southeast-1:123456789012:test-topic"
        };
        var publisher = new SnsAlertPublisher(options); // Không inject IAwsSnsClient

        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.CircuitBreakerOpen,
            Severity = IncidentSeverity.Critical,
            Title = "Live Alert",
            Details = "Live failure"
        };

        var result = await publisher.PublishIncidentAlertAsync(alert);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "IAwsSnsClient");
    }

    [TestMethod]
    public async Task SnsAlertPublisher_LiveMode_CallsAwsClientWhenConfigured()
    {
        var mockClient = new MockAwsSnsClient("mock-live-msg-999");
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = false, // Live Mode
            TopicArn = "arn:aws:sns:ap-southeast-1:123456789012:test-topic"
        };
        var publisher = new SnsAlertPublisher(options, mockClient);

        var alert = new IncidentAlert
        {
            IncidentType = IncidentTypes.CircuitBreakerOpen,
            Severity = IncidentSeverity.Critical,
            Title = "Live Alert",
            Details = "Live failure"
        };

        var result = await publisher.PublishIncidentAlertAsync(alert);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("mock-live-msg-999", result.MessageId);
        Assert.AreEqual(1, mockClient.PublishCallCount);
    }

    [TestMethod]
    public void JobMetrics_EmitsCloudWatchEmfLog_WhenCircuitBreakerTrips()
    {
        var logs = new List<string>();
        using var metrics = new JobMetrics("Staging", logs.Add);

        metrics.RecordCircuitBreakerState("AWS_BEDROCK", CircuitBreakerState.Open);

        Assert.IsTrue(logs.Any(l => l.Contains("_aws") && l.Contains("ai_provider_circuit_breaker_state")),
            "JobMetrics phải xuất bản log định dạng AWS CloudWatch EMF khi circuit breaker chuyển trạng thái.");
    }

    [TestMethod]
    public async Task JobStore_GetStoreMetricsSummary_CalculatesAccuratelyAcrossProcesses()
    {
        var store = new InMemoryJobStore();
        var request = JobRequest.Create("ExamGeneration", "payload-ref", "user-test-01", SchoolScope, "idemp-summary-01", "corr-summary-01");
        var job = await store.EnqueueAsync(request, "fingerprint-01");

        var claim = await store.ClaimNextJobAsync("worker-01", TimeSpan.FromMinutes(2));
        Assert.IsNotNull(claim);

        await store.CompleteJobAsync(job.Id, claim.LeaseToken, "exam-result-01");

        var summary = await store.GetStoreMetricsSummaryAsync();

        Assert.AreEqual(1L, summary.TotalJobs);
        Assert.AreEqual(1L, summary.CompletedJobs);
        Assert.AreEqual(0L, summary.FailedJobs);
        Assert.AreEqual(100.0, summary.SuccessRatePercentage);
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

    [TestMethod]
    public void JobMetrics_EmitsCloudWatchEmf_WithMultiDimensionSets_MatchingTerraformAlarm()
    {
        var logs = new List<string>();
        var metrics = new JobMetrics(environmentName: "Production", logAction: logs.Add);

        metrics.RecordJobFailed("ExamGeneration", "Permanent", "ERR_FATAL", 150);

        var emfJson = logs.FirstOrDefault(l => l.Contains("\"_aws\""));
        Assert.IsNotNull(emfJson, "CloudWatch EMF JSON line must be logged");

        using var doc = JsonDocument.Parse(emfJson);
        var root = doc.RootElement;

        Assert.AreEqual("Production", root.GetProperty("Environment").GetString());
        Assert.AreEqual("ExamGeneration", root.GetProperty("job_type").GetString());
        Assert.AreEqual(1L, root.GetProperty("jobs_failed_total").GetInt64());

        var awsElement = root.GetProperty("_aws");
        Assert.AreEqual("AiExamBank/Jobs", awsElement.GetProperty("CloudWatchMetrics")[0].GetProperty("Namespace").GetString());

        var dimensionsArray = awsElement.GetProperty("CloudWatchMetrics")[0].GetProperty("Dimensions");
        Assert.AreEqual(2, dimensionsArray.GetArrayLength(), "Must emit 2 dimension sets: [Environment] for Terraform Alarm and [Environment, job_type] for drill-down");

        var dimSet1 = dimensionsArray[0].EnumerateArray().Select(e => e.GetString()).ToList();
        var dimSet2 = dimensionsArray[1].EnumerateArray().Select(e => e.GetString()).ToList();

        CollectionAssert.AreEqual(new[] { "Environment" }, dimSet1);
        CollectionAssert.AreEqual(new[] { "Environment", "job_type" }, dimSet2);
    }

    [TestMethod]
    public async Task DefaultAwsSnsClient_PublishAsync_SendsXmlPayloadAndParsesMessageId()
    {
        var fakeResponseXml = """
            <PublishResponse xmlns="http://sns.amazonaws.com/doc/2010-03-31/">
                <PublishResult>
                    <MessageId>94f39b1b-cc91-5370-84a8-5055bb0ea3fe</MessageId>
                </PublishResult>
                <ResponseMetadata>
                    <RequestId>f18730e4-4ee3-5686-892e-aedbe9be1320</RequestId>
                </ResponseMetadata>
            </PublishResponse>
            """;

        var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, fakeResponseXml);
        using var httpClient = new HttpClient(fakeHandler);
        var options = new SnsAlertOptions
        {
            Region = "ap-southeast-1",
            TopicArn = "arn:aws:sns:ap-southeast-1:123456789012:reliability-alerts"
        };
        var client = new DefaultAwsSnsClient(httpClient, options);

        var messageId = await client.PublishAsync(
            options.TopicArn,
            "Critical Job Engine Alert Payload",
            "[CRITICAL] Circuit Breaker Tripped");

        Assert.AreEqual("94f39b1b-cc91-5370-84a8-5055bb0ea3fe", messageId);
        Assert.AreEqual("https://sns.ap-southeast-1.amazonaws.com/", fakeHandler.LastRequest?.RequestUri?.ToString());
        Assert.AreEqual(HttpMethod.Post, fakeHandler.LastRequest?.Method);
    }

    [TestMethod]
    public async Task DefaultAwsSnsClient_PublishAsync_ThrowsOnHttpError()
    {
        var fakeErrorXml = """
            <ErrorResponse xmlns="http://sns.amazonaws.com/doc/2010-03-31/">
                <Error>
                    <Type>Sender</Type>
                    <Code>NotFound</Code>
                    <Message>Topic does not exist</Message>
                </Error>
                <RequestId>b163d89e-561b-4395-9ffb-3bc7a1a2b75a</RequestId>
            </ErrorResponse>
            """;

        var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, fakeErrorXml);
        using var httpClient = new HttpClient(fakeHandler);
        var options = new SnsAlertOptions
        {
            Region = "ap-southeast-1",
            TopicArn = "arn:aws:sns:ap-southeast-1:123456789012:non-existent-topic"
        };
        var client = new DefaultAwsSnsClient(httpClient, options);

        try
        {
            await client.PublishAsync(options.TopicArn, "Alert content", "Test Subject");
            Assert.Fail("Expected HttpRequestException was not thrown.");
        }
        catch (HttpRequestException ex)
        {
            Assert.IsTrue(ex.Message.Contains("Topic does not exist") || ex.Message.Contains("NotFound"));
        }
    }

    [TestMethod]
    public async Task FileJobStore_CircuitBreakerState_And_RecentAlerts_PersistsAcrossInstances()
    {
        var tempStoreDir = Path.Combine(Path.GetTempPath(), "aiexam_store_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            // Instance 1: Ghi nhận Circuit Breaker và Alert
            var store1 = new FileJobStore(tempStoreDir);
            await store1.RecordCircuitBreakerStateAsync("AWS_BEDROCK", CircuitBreakerState.Open);

            var testAlert = new IncidentAlert
            {
                Title = "Bedrock Failure Spike",
                IncidentType = IncidentTypes.CircuitBreakerOpen,
                Severity = IncidentSeverity.Critical,
                Details = "5 consecutive timeouts",
                TimestampUtc = DateTimeOffset.UtcNow
            };
            await store1.RecordIncidentAlertAsync(testAlert);

            // Instance 2: Đọc dữ liệu từ cùng thư mục lưu trữ bền vững (giả lập liên tiến trình giữa Worker và API)
            var store2 = new FileJobStore(tempStoreDir);
            var cbStates = await store2.GetCircuitBreakerStatesAsync();
            var recentAlerts = await store2.GetRecentAlertsAsync();

            Assert.IsTrue(cbStates.ContainsKey("AWS_BEDROCK"));
            Assert.AreEqual("Open", cbStates["AWS_BEDROCK"]);

            Assert.HasCount(1, recentAlerts);
            Assert.AreEqual("Bedrock Failure Spike", recentAlerts[0].Title);
            Assert.AreEqual(IncidentTypes.CircuitBreakerOpen, recentAlerts[0].IncidentType);
        }
        finally
        {
            if (Directory.Exists(tempStoreDir))
            {
                Directory.Delete(tempStoreDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task JobService_GetReliabilitySummaryAsync_ReadsCircuitBreakerAndAlertsFromStore()
    {
        var tempStoreDir = Path.Combine(Path.GetTempPath(), "aiexam_store_summary_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileJobStore(tempStoreDir);
            await store.RecordCircuitBreakerStateAsync("AZURE_OPENAI", CircuitBreakerState.Open);

            var workerAlert = new IncidentAlert
            {
                Title = "Worker Trip Alert",
                IncidentType = IncidentTypes.CircuitBreakerOpen,
                Severity = IncidentSeverity.Critical,
                Details = "Tripped inside standalone worker process",
                TimestampUtc = DateTimeOffset.UtcNow
            };
            await store.RecordIncidentAlertAsync(workerAlert);

            var metrics = new JobMetrics();
            var alertPublisher = new SnsAlertPublisher(new SnsAlertOptions { IsEnabled = true, UseMockSimulation = true }, awsSnsClient: null, store: store);
            var jobService = new JobService(store, metrics, alertPublisher);

            var summary = await jobService.GetReliabilitySummaryAsync();

            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.CircuitBreakerStates.ContainsKey("AZURE_OPENAI"));
            Assert.AreEqual("Open", summary.CircuitBreakerStates["AZURE_OPENAI"]);
            Assert.IsTrue(summary.RecentAlerts.Any(a => a.Title == "Worker Trip Alert"));
        }
        finally
        {
            if (Directory.Exists(tempStoreDir))
            {
                Directory.Delete(tempStoreDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task SnsAlertPublisher_RateLimiting_AppliesToDrillAlerts()
    {
        var options = new SnsAlertOptions
        {
            IsEnabled = true,
            UseMockSimulation = true,
            DrillThrottleWindowSeconds = 60
        };
        var publisher = new SnsAlertPublisher(options);

        var alert1 = new IncidentAlert
        {
            IncidentType = IncidentTypes.DrillTestAlert,
            Severity = IncidentSeverity.Info,
            Title = "Drill attempt 1",
            Details = "Testing rate limit",
            TimestampUtc = DateTimeOffset.UtcNow
        };
        var firstResult = await publisher.PublishIncidentAlertAsync(alert1);
        Assert.IsTrue(firstResult.Success);
        Assert.IsFalse(firstResult.WasThrottled);

        var alert2 = new IncidentAlert
        {
            IncidentType = IncidentTypes.DrillTestAlert,
            Severity = IncidentSeverity.Info,
            Title = "Drill attempt 2 immediately",
            Details = "Testing rate limit throttling",
            TimestampUtc = DateTimeOffset.UtcNow
        };
        var secondResult = await publisher.PublishIncidentAlertAsync(alert2);
        Assert.IsTrue(secondResult.Success);
        Assert.IsTrue(secondResult.WasThrottled);
    }

    #endregion

    #region 5. Admin Authorization & Explicit Security Claims Tests

    [TestMethod]
    public void JobSecurityPolicies_UnauthenticatedUser_IsRejected()
    {
        Assert.IsFalse(JobSecurityPolicies.IsSystemAdministrator(null));

        var unauthenticatedPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity());
        Assert.IsFalse(JobSecurityPolicies.IsSystemAdministrator(unauthenticatedPrincipal));
    }

    [TestMethod]
    public void JobSecurityPolicies_UserWithAdminInUserId_WithoutAdminRole_IsRejected()
    {
        // Kiểm thử lỗ hổng CWE-285, CWE-863: UserId chứa chuỗi "admin" hoặc "ministry"
        // nhưng KHÔNG có role hay permission hợp lệ thì BẮT BUỘC phải bị từ chối!
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "fake-admin-user"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "ministry-hacker"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Teacher")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        Assert.IsFalse(
            JobSecurityPolicies.IsSystemAdministrator(principal),
            "Người dùng chỉ có chuỗi 'admin' hoặc 'ministry' trong tên/ID nhưng không có Role Admin không được cấp quyền.");
    }

    [TestMethod]
    public void JobSecurityPolicies_ExplicitAdminRoles_AreAuthorized()
    {
        // 1. Role Admin chuẩn
        var adminClaims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin") };
        var adminPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(adminClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(adminPrincipal));

        // 2. Role SystemAdmin chuẩn
        var sysAdminClaims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "SystemAdmin") };
        var sysAdminPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(sysAdminClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(sysAdminPrincipal));

        // 3. Claim "role" (JWT format)
        var jwtRoleClaims = new[] { new System.Security.Claims.Claim("role", "Admin") };
        var jwtRolePrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(jwtRoleClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(jwtRolePrincipal));
    }

    [TestMethod]
    public void JobSecurityPolicies_MinistryExamScope_IsAuthorized()
    {
        // ScopeKind cấp Bộ (MinistryExam) được phép truy cập
        var ministryClaims = new[] { new System.Security.Claims.Claim("scope_kind", nameof(JobScopeKind.MinistryExam)) };
        var ministryPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(ministryClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(ministryPrincipal));

        // ScopeKind cấp Trường (School) không được phép truy cập metrics toàn hệ thống
        var schoolClaims = new[] { new System.Security.Claims.Claim("scope_kind", nameof(JobScopeKind.School)) };
        var schoolPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(schoolClaims, "TestAuth"));
        Assert.IsFalse(JobSecurityPolicies.IsSystemAdministrator(schoolPrincipal));
    }

    [TestMethod]
    public void JobSecurityPolicies_SpecificPermissions_AreAuthorized()
    {
        // Permission jobs.metrics
        var metricsClaims = new[] { new System.Security.Claims.Claim("permission", JobSecurityPolicies.MetricsPermission) };
        var metricsPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(metricsClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(metricsPrincipal));

        // Permission jobs.admin
        var adminClaims = new[] { new System.Security.Claims.Claim("permission", JobSecurityPolicies.AdminPermission) };
        var adminPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(adminClaims, "TestAuth"));
        Assert.IsTrue(JobSecurityPolicies.IsSystemAdministrator(adminPrincipal));

        // Permission không liên quan
        var otherClaims = new[] { new System.Security.Claims.Claim("permission", "jobs.read") };
        var otherPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(otherClaims, "TestAuth"));
        Assert.IsFalse(JobSecurityPolicies.IsSystemAdministrator(otherPrincipal));
    }

    #endregion

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class MockAwsSnsClient : IAwsSnsClient
    {
        private readonly string _returnedMessageId;
        public int PublishCallCount { get; private set; }

        public MockAwsSnsClient(string returnedMessageId)
        {
            _returnedMessageId = returnedMessageId;
        }

        public Task<string> PublishAsync(string topicArn, string message, string? subject = null, CancellationToken cancellationToken = default)
        {
            PublishCallCount++;
            return Task.FromResult(_returnedMessageId);
        }
    }
}
