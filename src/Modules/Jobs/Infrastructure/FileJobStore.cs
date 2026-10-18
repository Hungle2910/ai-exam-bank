using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Kho lưu trữ tác vụ nền bền vững theo tệp tin (File-backed Durable Job Store).
/// Đảm bảo dữ liệu hàng đợi không bị mất khi tiến trình Worker hoặc API khởi động lại (Persistence across restart),
/// và cho phép các tiến trình độc lập (API Host và Worker Host) chia sẻ chung trạng thái tác vụ thông qua cơ chế khóa liên tiến trình (Cross-process Mutex).
/// </summary>
public sealed class FileJobStore : IJobStore, IDisposable
{
    /// <summary>
    /// Phiên bản lược đồ lưu trữ hiện tại.
    /// Phiên bản 2: Chuẩn hóa ResourceId trong Fingerprint và IdempotencyIndex theo ToUpperInvariant().
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _syncRoot = new();
    private readonly string _filePath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Mutex _processMutex;
    private DateTime _lastFileWriteTime = DateTime.MinValue;
    private long _lastFileLength = -1;

    private Dictionary<Guid, BackgroundJob> _jobs = new();
    private Dictionary<string, Guid> _idempotencyIndex = new();
    private Dictionary<Guid, List<JobAttempt>> _attempts = new();

    /// <summary>
    /// DTO lưu trữ thông tin Job trên đĩa, tương thích lược đồ bảng PostgreSQL jobs.
    /// </summary>
    internal sealed class JobDataDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public JobStatus Status { get; set; }
        public string PayloadReference { get; set; } = string.Empty;
        public string? ResultId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public JobScopeKind ScopeKind { get; set; }
        public string ScopeResourceId { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RequestFingerprint { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTimeOffset? AvailableAt { get; set; }
        public DateTimeOffset? CancellationRequestedAt { get; set; }
        public int AttemptCount { get; set; }
        public int? ProgressPercentage { get; set; }
        public string? ErrorCode { get; set; }
        public string? SafeErrorMessage { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO lưu trữ thông tin Attempt trên đĩa, tương thích lược đồ bảng PostgreSQL job_attempts.
    /// </summary>
    internal sealed class AttemptDataDto
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public int AttemptNumber { get; set; }
        public AttemptStatus Status { get; set; }
        public string WorkerId { get; set; } = string.Empty;
        public Guid LeaseToken { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset HeartbeatAt { get; set; }
        public DateTimeOffset LeaseExpiresAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? ErrorCode { get; set; }
        public string? SafeErrorMessage { get; set; }
    }

    /// <summary>
    /// Mô hình dữ liệu lưu trữ trên đĩa JSON.
    /// </summary>
    internal sealed class StoreSnapshot
    {
        public int? SchemaVersion { get; set; }
        public List<JobDataDto> Jobs { get; set; } = new();
        public Dictionary<string, Guid> IdempotencyIndex { get; set; } = new();
        public Dictionary<Guid, List<AttemptDataDto>> Attempts { get; set; } = new();
    }

    /// <summary>
    /// Khởi tạo FileJobStore với đường dẫn tệp tin và đồng hồ thời gian tùy chọn.
    /// </summary>
    /// <param name="filePath">Đường dẫn tệp tin JSON (mặc định lưu tại jobs_store.json cùng thư mục ứng dụng).</param>
    /// <param name="clock">Hàm lấy thời gian hiện tại (mặc định DateTimeOffset.UtcNow).</param>
    public FileJobStore(string? filePath = null, Func<DateTimeOffset>? clock = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "jobs_store.json");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        // Mutex đồng bộ hóa truy cập giữa các process độc lập (API và Worker)
        var mutexName = "Global\\AiExamBank_JobStore_" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_filePath)))[..16];
        _processMutex = new Mutex(false, mutexName);

        // Nạp snapshot và thực hiện migration có bảo vệ bằng Mutex chống xung đột lost-update khi khởi động đồng thời
        ExecuteWithLock(() => true);
    }

    /// <inheritdoc />
    public Task<BackgroundJob> EnqueueAsync(JobRequest request, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("Request fingerprint cannot be empty.", nameof(requestFingerprint));

        cancellationToken.ThrowIfCancellationRequested();
        var idempotencyKey = BuildIdempotencyKey(request);
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId))
            {
                var existingJob = _jobs[existingId];
                if (existingJob.RequestFingerprint == requestFingerprint)
                {
                    return CloneJob(existingJob);
                }

                throw new JobConflictException(
                    $"Job with idempotency key '{request.IdempotencyKey}' already exists with a different request fingerprint.");
            }

            var newJob = new BackgroundJob
            {
                Id = Guid.NewGuid(),
                Type = request.JobType,
                Status = JobStatus.Pending,
                PayloadReference = request.PayloadReference,
                ResultId = null,
                CreatedBy = request.RequesterId,
                Scope = request.Scope,
                IdempotencyKey = request.IdempotencyKey,
                RequestFingerprint = requestFingerprint,
                CorrelationId = request.CorrelationId,
                AvailableAt = now,
                CancellationRequestedAt = null,
                AttemptCount = 0,
                ProgressPercentage = null,
                ErrorCode = null,
                SafeErrorMessage = null,
                CreatedAt = now,
                UpdatedAt = now
            };

            _jobs[newJob.Id] = newJob;
            _idempotencyIndex[idempotencyKey] = newJob.Id;
            _attempts[newJob.Id] = new List<JobAttempt>();

            SaveToDisk();
            return CloneJob(newJob);
        });
    }

    /// <inheritdoc />
    public Task<BackgroundJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteWithLock(() =>
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                return CloneJob(job);
            }

            return null;
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BackgroundJob>> ListByScopeAsync(JobScope scope, JobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteWithLock(() =>
        {
            var results = _jobs.Values
                .Where(j => j.Scope.Kind == scope.Kind &&
                            string.Equals(j.Scope.ResourceId, scope.ResourceId, StringComparison.OrdinalIgnoreCase))
                .Where(j => !statusFilter.HasValue || j.Status == statusFilter.Value)
                .OrderByDescending(j => j.CreatedAt)
                .Select(CloneJob)
                .ToList();

            return (IReadOnlyList<BackgroundJob>)results;
        });
    }

    /// <inheritdoc />
    public Task<JobClaimResult?> ClaimNextJobAsync(string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID cannot be empty.", nameof(workerId));

        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            // 1. Tìm tác vụ Pending đã đến hạn chạy (AvailableAt <= now)
            var eligibleJob = _jobs.Values
                .Where(j => j.Status == JobStatus.Pending && (j.AvailableAt == null || j.AvailableAt <= now))
                .OrderBy(j => j.AvailableAt ?? j.CreatedAt)
                .FirstOrDefault();

            // 2. Nếu không có Pending, tìm tác vụ Running nhưng đã bị bỏ hoang (Lease quá hạn)
            var hasModifications = false;
            if (eligibleJob is null)
            {
                foreach (var runningJob in _jobs.Values.Where(j => j.Status == JobStatus.Running))
                {
                    if (_attempts.TryGetValue(runningJob.Id, out var jobAttempts))
                    {
                        var lastAttempt = jobAttempts.LastOrDefault();
                        if (lastAttempt != null && lastAttempt.Status == AttemptStatus.Started && lastAttempt.LeaseExpiresAt < now)
                        {
                            lastAttempt.Status = AttemptStatus.Abandoned;
                            lastAttempt.FinishedAt = now;
                            hasModifications = true;

                            if (runningJob.AttemptCount >= 3)
                            {
                                runningJob.Status = JobStatus.Failed;
                                runningJob.ErrorCode = "RETRY_BUDGET_EXHAUSTED";
                                runningJob.SafeErrorMessage = "Job exceeded maximum of 3 attempts after lease expiration.";
                                runningJob.UpdatedAt = now;
                                continue;
                            }

                            var retryDelayMinutes = runningJob.AttemptCount == 1 ? 1 : 5;
                            runningJob.Status = JobStatus.Pending;
                            runningJob.AvailableAt = now.AddMinutes(retryDelayMinutes);
                            runningJob.UpdatedAt = now;
                            continue;
                        }
                    }
                }
            }

            if (eligibleJob is null)
            {
                if (hasModifications)
                {
                    SaveToDisk();
                }
                return (JobClaimResult?)null;
            }

            // Tiến hành Claim tác vụ
            var attemptNumber = eligibleJob.AttemptCount + 1;
            var leaseToken = Guid.NewGuid();
            var leaseExpiresAt = now.Add(leaseDuration);

            var attempt = new JobAttempt
            {
                Id = Guid.NewGuid(),
                JobId = eligibleJob.Id,
                AttemptNumber = attemptNumber,
                Status = AttemptStatus.Started,
                WorkerId = workerId,
                LeaseToken = leaseToken,
                StartedAt = now,
                HeartbeatAt = now,
                LeaseExpiresAt = leaseExpiresAt,
                FinishedAt = null,
                ErrorCode = null,
                SafeErrorMessage = null
            };

            eligibleJob.Status = JobStatus.Running;
            eligibleJob.AttemptCount = attemptNumber;
            eligibleJob.UpdatedAt = now;

            if (!_attempts.TryGetValue(eligibleJob.Id, out var list))
            {
                list = new List<JobAttempt>();
                _attempts[eligibleJob.Id] = list;
            }
            list.Add(attempt);

            SaveToDisk();

            return new JobClaimResult(
                CloneJob(eligibleJob),
                CloneAttempt(attempt),
                leaseToken);
        });
    }

    /// <inheritdoc />
    public Task<bool> HeartbeatAsync(Guid jobId, Guid leaseToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return false;

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return false;

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return false;

            currentAttempt.HeartbeatAt = now;
            currentAttempt.LeaseExpiresAt = now.Add(leaseDuration);
            job.UpdatedAt = now;

            SaveToDisk();
            return true;
        });
    }

    /// <inheritdoc />
    public Task<bool> UpdateProgressAsync(Guid jobId, Guid leaseToken, int progressPercentage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return false;

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return false;

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return false;

            job.ProgressPercentage = Math.Clamp(progressPercentage, 0, 100);
            job.UpdatedAt = now;

            SaveToDisk();
            return true;
        });
    }

    /// <inheritdoc />
    public Task<bool> CompleteJobAsync(Guid jobId, Guid leaseToken, string? resultId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return false;

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return false;

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return false;

            currentAttempt.Status = AttemptStatus.Completed;
            currentAttempt.FinishedAt = now;

            job.Status = JobStatus.Completed;
            job.ResultId = resultId;
            job.ProgressPercentage = 100;
            job.UpdatedAt = now;

            SaveToDisk();
            return true;
        });
    }

    /// <inheritdoc />
    public Task<bool> FailJobAsync(Guid jobId, Guid leaseToken, string errorCode, string safeErrorMessage, bool isTransient, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (!_jobs.TryGetValue(jobId, out var job) || job.Status != JobStatus.Running)
                return false;

            if (!_attempts.TryGetValue(jobId, out var attempts))
                return false;

            var currentAttempt = attempts.LastOrDefault();
            if (currentAttempt is null || currentAttempt.LeaseToken != leaseToken || currentAttempt.Status != AttemptStatus.Started)
                return false;

            currentAttempt.Status = AttemptStatus.Failed;
            currentAttempt.ErrorCode = errorCode;
            currentAttempt.SafeErrorMessage = safeErrorMessage;
            currentAttempt.FinishedAt = now;

            if (isTransient && job.AttemptCount < 3)
            {
                var retryDelayMinutes = job.AttemptCount == 1 ? 1 : 5;
                job.Status = JobStatus.Pending;
                job.AvailableAt = now.AddMinutes(retryDelayMinutes);
                job.ErrorCode = errorCode;
                job.SafeErrorMessage = safeErrorMessage;
                job.UpdatedAt = now;
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.ErrorCode = isTransient && job.AttemptCount >= 3 ? "RETRY_BUDGET_EXHAUSTED" : errorCode;
                job.SafeErrorMessage = safeErrorMessage;
                job.UpdatedAt = now;
            }

            SaveToDisk();
            return true;
        });
    }

    /// <inheritdoc />
    public Task<bool> RequestCancellationAsync(Guid jobId, string requesterId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            if (job.Status == JobStatus.Cancelled)
                return true;

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed)
                return false;

            if (job.Status == JobStatus.Pending)
            {
                job.Status = JobStatus.Cancelled;
                job.CancellationRequestedAt = now;
                job.UpdatedAt = now;
                SaveToDisk();
                return true;
            }

            if (job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Cancelled;
                job.CancellationRequestedAt = now;
                job.UpdatedAt = now;

                if (_attempts.TryGetValue(jobId, out var attempts))
                {
                    var currentAttempt = attempts.LastOrDefault();
                    if (currentAttempt != null && currentAttempt.Status == AttemptStatus.Started)
                    {
                        currentAttempt.Status = AttemptStatus.Failed;
                        currentAttempt.ErrorCode = "CANCELLED";
                        currentAttempt.SafeErrorMessage = "Job execution was cancelled by user.";
                        currentAttempt.FinishedAt = now;
                    }
                }

                SaveToDisk();
                return true;
            }

            return false;
        });
    }

    /// <inheritdoc />
    public Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock();

        return ExecuteWithLock(() =>
        {
            var recoveredCount = 0;

            foreach (var job in _jobs.Values.Where(j => j.Status == JobStatus.Running))
            {
                if (!_attempts.TryGetValue(job.Id, out var attempts))
                    continue;

                var lastAttempt = attempts.LastOrDefault();
                if (lastAttempt != null && lastAttempt.Status == AttemptStatus.Started && lastAttempt.LeaseExpiresAt < now)
                {
                    lastAttempt.Status = AttemptStatus.Abandoned;
                    lastAttempt.FinishedAt = now;
                    recoveredCount++;

                    if (job.AttemptCount >= 3)
                    {
                        job.Status = JobStatus.Failed;
                        job.ErrorCode = "RETRY_BUDGET_EXHAUSTED";
                        job.SafeErrorMessage = "Job attempt limit reached after lease expiration.";
                        job.UpdatedAt = now;
                    }
                    else
                    {
                        var retryDelayMinutes = job.AttemptCount == 1 ? 1 : 5;
                        job.Status = JobStatus.Pending;
                        job.AvailableAt = now.AddMinutes(retryDelayMinutes);
                        job.UpdatedAt = now;
                    }
                }
            }

            if (recoveredCount > 0)
            {
                SaveToDisk();
            }

            return recoveredCount;
        });
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các lượt Attempt của một Job (dùng cho thanh tra và kiểm thử).
    /// </summary>
    /// <param name="jobId">Mã định danh tác vụ.</param>
    /// <returns>Danh sách các bản ghi JobAttempt.</returns>
    public IReadOnlyList<JobAttempt> GetAttempts(Guid jobId)
    {
        return ExecuteWithLock(() =>
        {
            if (_attempts.TryGetValue(jobId, out var list))
            {
                return (IReadOnlyList<JobAttempt>)list.Select(CloneAttempt).ToList();
            }

            return Array.Empty<JobAttempt>();
        }).GetAwaiter().GetResult();
    }

    private Task<T> ExecuteWithLock<T>(Func<T> action)
    {
        lock (_syncRoot)
        {
            var hasMutex = false;
            try
            {
                try
                {
                    hasMutex = _processMutex.WaitOne(TimeSpan.FromSeconds(5));
                }
                catch (AbandonedMutexException)
                {
                    hasMutex = true;
                }

                if (!hasMutex)
                {
                    throw new TimeoutException("Could not acquire cross-process job store lock.");
                }

                // Luôn đồng bộ dữ liệu mới nhất nếu tệp trên đĩa vừa bị process khác ghi đè
                CheckAndReloadFromDisk();

                try
                {
                    var result = action();
                    return Task.FromResult(result);
                }
                catch
                {
                    _lastFileWriteTime = DateTime.MinValue;
                    _lastFileLength = -1;
                    throw;
                }
            }
            finally
            {
                if (hasMutex)
                {
                    _processMutex.ReleaseMutex();
                }
            }
        }
    }

    private void CheckAndReloadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            if (_jobs.Count > 0 || _idempotencyIndex.Count > 0 || _attempts.Count > 0)
            {
                _jobs.Clear();
                _idempotencyIndex.Clear();
                _attempts.Clear();
                _lastFileWriteTime = DateTime.MinValue;
                _lastFileLength = -1;
            }
            return;
        }

        var lastWrite = File.GetLastWriteTimeUtc(_filePath);
        var length = new FileInfo(_filePath).Length;
        if (lastWrite == _lastFileWriteTime && length == _lastFileLength)
        {
            return;
        }

        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            _jobs.Clear();
            _idempotencyIndex.Clear();
            _attempts.Clear();
            _lastFileWriteTime = DateTime.MinValue;
            _lastFileLength = -1;
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<StoreSnapshot>(json, JsonOptions);
            if (snapshot is null)
            {
                throw new InvalidOperationException("Job store snapshot is null or invalid.");
            }

            var migrated = MigrateSnapshot(snapshot);

            _jobs = snapshot.Jobs.ToDictionary(
                dto => dto.Id,
                dto => new BackgroundJob
                {
                    Id = dto.Id,
                    Type = dto.Type,
                    Status = dto.Status,
                    PayloadReference = dto.PayloadReference,
                    ResultId = dto.ResultId,
                    CreatedBy = dto.CreatedBy,
                    Scope = JobScope.Create(dto.ScopeKind, dto.ScopeResourceId),
                    IdempotencyKey = dto.IdempotencyKey,
                    RequestFingerprint = dto.RequestFingerprint,
                    CorrelationId = dto.CorrelationId,
                    AvailableAt = dto.AvailableAt,
                    CancellationRequestedAt = dto.CancellationRequestedAt,
                    AttemptCount = dto.AttemptCount,
                    ProgressPercentage = dto.ProgressPercentage,
                    ErrorCode = dto.ErrorCode,
                    SafeErrorMessage = dto.SafeErrorMessage,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt
                });

            _idempotencyIndex = snapshot.IdempotencyIndex ?? new Dictionary<string, Guid>();
            _attempts = (snapshot.Attempts ?? new Dictionary<Guid, List<AttemptDataDto>>())
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(a => new JobAttempt
                    {
                        Id = a.Id,
                        JobId = a.JobId,
                        AttemptNumber = a.AttemptNumber,
                        Status = a.Status,
                        WorkerId = a.WorkerId,
                        LeaseToken = a.LeaseToken,
                        StartedAt = a.StartedAt,
                        HeartbeatAt = a.HeartbeatAt,
                        LeaseExpiresAt = a.LeaseExpiresAt,
                        FinishedAt = a.FinishedAt,
                        ErrorCode = a.ErrorCode,
                        SafeErrorMessage = a.SafeErrorMessage
                    }).ToList());

            _lastFileWriteTime = File.GetLastWriteTimeUtc(_filePath);
            _lastFileLength = new FileInfo(_filePath).Length;

            if (migrated)
            {
                SaveToDisk();
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Failed to load the job store snapshot.", ex);
        }
    }

    private static bool MigrateSnapshot(StoreSnapshot snapshot)
    {
        var originalVersion = snapshot.SchemaVersion ?? 1;
        if (originalVersion >= CurrentSchemaVersion)
        {
            return false;
        }

        // SchemaVersion 1 -> 2 Migration:
        // Chuẩn hóa ResourceId theo quy tắc không phân biệt hoa thường (ToUpperInvariant)
        // cho cả mã băm RequestFingerprint và các khóa trong IdempotencyIndex.
        var newIndex = new Dictionary<string, Guid>();
        foreach (var job in snapshot.Jobs)
        {
            job.RequestFingerprint = ComputeRequestFingerprint(job.Type, job.PayloadReference, job.ScopeKind, job.ScopeResourceId);
            var key = BuildIdempotencyKey(job.ScopeKind, job.ScopeResourceId, job.CreatedBy, job.Type, job.IdempotencyKey);
            newIndex[key] = job.Id;
        }

        snapshot.IdempotencyIndex = newIndex;
        snapshot.SchemaVersion = CurrentSchemaVersion;
        return true;
    }

    private void SaveToDisk()
    {
        var snapshot = new StoreSnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            Jobs = _jobs.Values.Select(job => new JobDataDto
            {
                Id = job.Id,
                Type = job.Type,
                Status = job.Status,
                PayloadReference = job.PayloadReference,
                ResultId = job.ResultId,
                CreatedBy = job.CreatedBy,
                ScopeKind = job.Scope.Kind,
                ScopeResourceId = job.Scope.ResourceId,
                IdempotencyKey = job.IdempotencyKey,
                RequestFingerprint = job.RequestFingerprint,
                CorrelationId = job.CorrelationId,
                AvailableAt = job.AvailableAt,
                CancellationRequestedAt = job.CancellationRequestedAt,
                AttemptCount = job.AttemptCount,
                ProgressPercentage = job.ProgressPercentage,
                ErrorCode = job.ErrorCode,
                SafeErrorMessage = job.SafeErrorMessage,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            }).ToList(),
            IdempotencyIndex = _idempotencyIndex,
            Attempts = _attempts.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(a => new AttemptDataDto
                {
                    Id = a.Id,
                    JobId = a.JobId,
                    AttemptNumber = a.AttemptNumber,
                    Status = a.Status,
                    WorkerId = a.WorkerId,
                    LeaseToken = a.LeaseToken,
                    StartedAt = a.StartedAt,
                    HeartbeatAt = a.HeartbeatAt,
                    LeaseExpiresAt = a.LeaseExpiresAt,
                    FinishedAt = a.FinishedAt,
                    ErrorCode = a.ErrorCode,
                    SafeErrorMessage = a.SafeErrorMessage
                }).ToList())
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = _filePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
            _lastFileWriteTime = File.GetLastWriteTimeUtc(_filePath);
            _lastFileLength = new FileInfo(_filePath).Length;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Nuốt lỗi khi dọn file tạm
                }
            }
            throw;
        }
    }

    internal static string ComputeRequestFingerprint(string jobType, string payloadReference, JobScopeKind scopeKind, string scopeResourceId)
    {
        var normalizedResourceId = scopeResourceId.ToUpperInvariant();
        var rawInput = $"{jobType}:{payloadReference}:{scopeKind}:{normalizedResourceId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawInput));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal static string BuildIdempotencyKey(JobScopeKind scopeKind, string scopeResourceId, string requesterId, string jobType, string idempotencyKey) =>
        $"{scopeKind}:{scopeResourceId.ToUpperInvariant()}:{requesterId}:{jobType}:{idempotencyKey}";

    private static string BuildIdempotencyKey(JobRequest request) =>
        BuildIdempotencyKey(request.Scope.Kind, request.Scope.ResourceId, request.RequesterId, request.JobType, request.IdempotencyKey);

    private static BackgroundJob CloneJob(BackgroundJob job) => new()
    {
        Id = job.Id,
        Type = job.Type,
        Status = job.Status,
        PayloadReference = job.PayloadReference,
        ResultId = job.ResultId,
        CreatedBy = job.CreatedBy,
        Scope = job.Scope,
        IdempotencyKey = job.IdempotencyKey,
        RequestFingerprint = job.RequestFingerprint,
        CorrelationId = job.CorrelationId,
        AvailableAt = job.AvailableAt,
        CancellationRequestedAt = job.CancellationRequestedAt,
        AttemptCount = job.AttemptCount,
        ProgressPercentage = job.ProgressPercentage,
        ErrorCode = job.ErrorCode,
        SafeErrorMessage = job.SafeErrorMessage,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt
    };

    private static JobAttempt CloneAttempt(JobAttempt attempt) => new()
    {
        Id = attempt.Id,
        JobId = attempt.JobId,
        AttemptNumber = attempt.AttemptNumber,
        Status = attempt.Status,
        WorkerId = attempt.WorkerId,
        LeaseToken = attempt.LeaseToken,
        StartedAt = attempt.StartedAt,
        HeartbeatAt = attempt.HeartbeatAt,
        LeaseExpiresAt = attempt.LeaseExpiresAt,
        FinishedAt = attempt.FinishedAt,
        ErrorCode = attempt.ErrorCode,
        SafeErrorMessage = attempt.SafeErrorMessage
    };

    /// <summary>
    /// Giải phóng Mutex đồng bộ liên tiến trình.
    /// </summary>
    public void Dispose()
    {
        _processMutex.Dispose();
    }
}
