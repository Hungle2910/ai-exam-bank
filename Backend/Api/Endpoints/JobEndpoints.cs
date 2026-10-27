using System.Diagnostics;
using AiExamBank.Api.Authentication;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;

namespace AiExamBank.Api.Endpoints;

/// <summary>
/// DTO tiếp nhận yêu cầu đẩy một tác vụ ngầm mới vào hàng đợi.
/// </summary>
/// <param name="JobType">Tên loại tác vụ (ví dụ: "ExamGeneration", "SampleJob").</param>
/// <param name="PayloadReference">Tham chiếu dữ liệu đầu vào (ví dụ: đường dẫn S3, mã đợt import).</param>
/// <param name="ScopeKind">Loại phạm vi phân quyền ("School" hoặc "MinistryExam").</param>
/// <param name="ScopeResourceId">Mã trường hoặc mã kỳ thi Bộ.</param>
/// <param name="IdempotencyKey">Khóa chống trùng lặp do client sinh.</param>
public sealed record CreateJobApiRequest(
    string JobType,
    string PayloadReference,
    string ScopeKind,
    string ScopeResourceId,
    string IdempotencyKey);

/// <summary>
/// DTO tiếp nhận yêu cầu thử lại (Retry) tác vụ ngầm.
/// </summary>
/// <param name="Reason">Lý do thử lại (tùy chọn).</param>
public sealed record RetryJobApiRequest(string? Reason = null);

/// <summary>
/// DTO phản hồi chi tiết lượt chạy (Attempt) của tác vụ cho Status UI.
/// </summary>
public sealed record JobAttemptApiResponse(
    Guid Id,
    Guid JobId,
    int AttemptNumber,
    string Status,
    string WorkerId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    long? ExecutionDurationMs,
    string? ErrorCode,
    string? SafeErrorMessage,
    string? RetryReason,
    bool IsManualRetry);

/// <summary>
/// DTO phản hồi trạng thái an toàn của tác vụ ngầm cho Client và Status UI.
/// </summary>
/// <param name="JobId">Mã định danh duy nhất của tác vụ.</param>
/// <param name="JobType">Loại tác vụ được lưu trong kho job.</param>
/// <param name="Status">Trạng thái vòng đời hiện tại của tác vụ.</param>
/// <param name="ProgressPercentage">Tiến độ thực tế (0 - 100%) hoặc null nếu không đo lường được.</param>
/// <param name="ResultId">Mã kết quả sinh ra nếu tác vụ thành công.</param>
/// <param name="ErrorCode">Mã lỗi an toàn nếu thất bại.</param>
/// <param name="SafeMessage">Thông báo an toàn hiển thị cho người dùng.</param>
public sealed record JobApiResponse(
    Guid JobId,
    string JobType,
    string Status,
    int? ProgressPercentage,
    string? ResultId,
    string? ErrorCode,
    string? SafeMessage,
    bool? IsRetryEligible = null,
    string? ActionableReason = null);

/// <summary>
/// Định nghĩa các HTTP Endpoints quản lý và tra cứu tác vụ ngầm thuộc chuẩn M5-02.
/// </summary>
public static class JobEndpoints
{
    /// <summary>
    /// Đăng ký toàn bộ các endpoint quản trị và tra cứu tác vụ nền theo chuẩn RESTful /api/v1/jobs.
    /// </summary>
    /// <param name="routes">Endpoint route builder của ASP.NET Core.</param>
    /// <returns>RouteGroupBuilder đã cấu hình các route.</returns>
    public static RouteGroupBuilder MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/jobs")
            .WithTags("Jobs")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = JobEngineAuthenticationHandler.SchemeName
            });

        // 1. POST /api/v1/jobs - Đẩy tác vụ mới vào hàng đợi (Idempotent Enqueue)
        group.MapPost("/", async (
            [FromBody] CreateJobApiRequest request,
            [FromHeader(Name = "X-Correlation-Id")] string? headerCorrelationId,
            [FromServices] IJobPublisher publisher,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var correlationId = string.IsNullOrWhiteSpace(headerCorrelationId)
                ? Activity.Current?.Id ?? httpContext.TraceIdentifier
                : headerCorrelationId;

            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";

            if (!Enum.TryParse<JobScopeKind>(request.ScopeKind, ignoreCase: true, out var scopeKind) || !Enum.IsDefined(scopeKind))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Kind",
                    detail: $"Scope kind '{request.ScopeKind}' is invalid. Supported values: School, MinistryExam.");
            }

            if (string.IsNullOrWhiteSpace(request.ScopeResourceId))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Resource Id",
                    detail: "Scope resource ID cannot be empty or whitespace.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_SCOPE",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            var authenticatedScope = ResolveAuthenticatedScope(httpContext);
            if (authenticatedScope is null ||
                authenticatedScope.Kind != scopeKind ||
                !string.Equals(authenticatedScope.ResourceId, request.ScopeResourceId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Caller identity is not authorized to create jobs in the requested scope.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            try
            {
                var scope = JobScope.Create(scopeKind, request.ScopeResourceId);
                var jobRequest = JobRequest.Create(
                    request.JobType,
                    request.PayloadReference,
                    requesterId,
                    scope,
                    request.IdempotencyKey,
                    correlationId);

                var jobRef = await publisher.EnqueueAsync(jobRequest, cancellationToken);

                return Results.Accepted(
                    uri: $"/api/v1/jobs/{jobRef.Value}",
                    value: new
                    {
                        jobId = jobRef.Value,
                        status = "Pending",
                        correlationId
                    });
            }
            catch (JobConflictException conflictEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Idempotency Conflict",
                    detail: conflictEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "IDEMPOTENCY_CONFLICT",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (ArgumentException argEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error",
                    detail: argEx.Message);
            }
        });

        // 2. GET /api/v1/jobs/{id} - Lấy chi tiết trạng thái tác vụ
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";
            var authenticatedScope = ResolveAuthenticatedScope(httpContext);

            if (authenticatedScope is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Authenticated scope claim is required to access scoped jobs.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (id == Guid.Empty)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: "Job ID cannot be empty GUID.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_JOB_ID",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            var jobRef = JobRef.Create(id);

            try
            {
                var status = await tracker.GetJobStatusAsync(jobRef, requesterId, authenticatedScope, cancellationToken);

                return Results.Ok(new JobApiResponse(
                    status.Job.Value,
                    status.JobType,
                    status.Status.ToString(),
                    status.ProgressPercentage,
                    status.ResultId,
                    status.ErrorCode,
                    status.SafeMessage,
                    status.IsRetryEligible,
                    status.ActionableReason));
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Job Not Found",
                    detail: $"Background job with ID '{id}' was not found.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (UnauthorizedAccessException authEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: authEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
        });

        // 3. GET /api/v1/jobs - Danh sách tác vụ trong phạm vi
        group.MapGet("/", async (
            [FromQuery] string scopeKind,
            [FromQuery] string scopeResourceId,
            [FromQuery] string? status,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";

            // Xác định phạm vi được phép từ danh tính đã xác thực (ClaimsPrincipal)
            var authenticatedScope = ResolveAuthenticatedScope(httpContext);

            if (authenticatedScope is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Authenticated scope claim is required to list scoped jobs.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (!Enum.TryParse<JobScopeKind>(scopeKind, ignoreCase: true, out var parsedKind) || !Enum.IsDefined(parsedKind))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Kind",
                    detail: $"Scope kind '{scopeKind}' is invalid. Supported values: School, MinistryExam.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_SCOPE",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (string.IsNullOrWhiteSpace(scopeResourceId))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Resource Id",
                    detail: "Scope resource ID cannot be empty or whitespace.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_SCOPE",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            // Kiểm tra phân quyền: Không cho phép người dùng truy cập danh sách job của scope khác (cả Kind và ResourceId)
            if (authenticatedScope.Kind != parsedKind ||
                !string.Equals(authenticatedScope.ResourceId, scopeResourceId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Caller identity is not authorized to list jobs in the requested scope.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            JobStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsedStatus) && Enum.IsDefined(parsedStatus))
            {
                statusFilter = parsedStatus;
            }

            try
            {
                var scope = JobScope.Create(parsedKind, scopeResourceId);
                var jobs = await tracker.ListJobsAsync(scope, requesterId, authenticatedScope, statusFilter, cancellationToken);

                var response = jobs.Select(j => new JobApiResponse(
                    j.Job.Value,
                    j.JobType,
                    j.Status.ToString(),
                    j.ProgressPercentage,
                    j.ResultId,
                    j.ErrorCode,
                    j.SafeMessage,
                    j.IsRetryEligible,
                    j.ActionableReason)).ToList();

                return Results.Ok(response);
            }
            catch (ArgumentException argEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Request",
                    detail: argEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_SCOPE",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (UnauthorizedAccessException authEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: authEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
        });

        // 4. POST /api/v1/jobs/{id}/cancel - Yêu cầu hủy tác vụ an toàn
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";
            var authenticatedScope = ResolveAuthenticatedScope(httpContext);

            if (authenticatedScope is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Authenticated scope claim is required to cancel scoped jobs.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            if (id == Guid.Empty)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: "Job ID cannot be empty GUID.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INVALID_JOB_ID",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            var jobRef = JobRef.Create(id);

            try
            {
                var cancelled = await tracker.CancelJobAsync(jobRef, requesterId, authenticatedScope, cancellationToken);

                if (!cancelled)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Cancellation Rejected",
                        detail: "The job cannot be cancelled because it has already completed or failed.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "JOB_CANNOT_BE_CANCELLED",
                            ["traceId"] = httpContext.TraceIdentifier
                        });
                }

                return Results.Ok(new
                {
                    success = true,
                    message = $"Cancellation requested for job '{id}'."
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Job Not Found",
                    detail: $"Background job with ID '{id}' was not found.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "JOB_NOT_FOUND",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (UnauthorizedAccessException authEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: authEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
        });

        // 5. POST /api/v1/jobs/{id}/retry - Yêu cầu thử lại tác vụ an toàn với kiểm tra Scoped Zero-Trust
        group.MapPost("/{id:guid}/retry", async (
            [FromRoute] Guid id,
            [FromBody] RetryJobApiRequest? request,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";
            var authenticatedScope = ResolveAuthenticatedScope(httpContext);

            if (authenticatedScope is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Authenticated scope claim is required to retry scoped jobs.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            try
            {
                var retriedStatus = await tracker.RetryJobAsync(
                    JobRef.Create(id),
                    requesterId,
                    authenticatedScope,
                    request?.Reason,
                    cancellationToken);

                return Results.Ok(new JobApiResponse(
                    retriedStatus.Job.Value,
                    retriedStatus.JobType,
                    retriedStatus.Status.ToString(),
                    retriedStatus.ProgressPercentage,
                    retriedStatus.ResultId,
                    retriedStatus.ErrorCode,
                    retriedStatus.SafeMessage,
                    retriedStatus.IsRetryEligible,
                    retriedStatus.ActionableReason));
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Job Not Found",
                    detail: $"Background job with ID '{id}' was not found.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "JOB_NOT_FOUND",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (UnauthorizedAccessException authEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: authEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (InvalidOperationException opEx)
            {
                var isCompleted = opEx.Message.Contains("completed", StringComparison.OrdinalIgnoreCase);
                return Results.Problem(
                    statusCode: isCompleted ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict,
                    title: isCompleted ? "Job Already Completed" : "Job Already Active",
                    detail: opEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = isCompleted ? "JOB_ALREADY_COMPLETED" : "JOB_ALREADY_ACTIVE",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
        });

        // 6. GET /api/v1/jobs/{id}/attempts - Lấy toàn bộ lịch sử các lượt chạy (Attempts History) của tác vụ
        group.MapGet("/{id:guid}/attempts", async (
            [FromRoute] Guid id,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = httpContext.User?.Identity?.Name ?? "system-user";
            var authenticatedScope = ResolveAuthenticatedScope(httpContext);

            if (authenticatedScope is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: "Authenticated scope claim is required to view scoped job attempts.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }

            try
            {
                var attempts = await tracker.GetJobAttemptsAsync(
                    JobRef.Create(id),
                    requesterId,
                    authenticatedScope,
                    cancellationToken);

                var response = attempts.Select(a => new JobAttemptApiResponse(
                    a.Id,
                    a.JobId,
                    a.AttemptNumber,
                    a.Status.ToString(),
                    a.WorkerId,
                    a.StartedAt,
                    a.FinishedAt,
                    a.ExecutionDurationMs,
                    a.ErrorCode,
                    a.SafeErrorMessage,
                    a.RetryReason,
                    a.IsManualRetry)).ToList();

                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Job Not Found",
                    detail: $"Background job with ID '{id}' was not found.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "JOB_NOT_FOUND",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
            catch (UnauthorizedAccessException authEx)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access Denied",
                    detail: authEx.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "SCOPED_ACCESS_DENIED",
                        ["traceId"] = httpContext.TraceIdentifier
                    });
            }
        });

        // 7. GET /api/v1/jobs/dev-token - Cấp token phát triển tạm thời cho UI Dashboard (chỉ khả dụng trong môi trường Development)
        routes.MapGet("/api/v1/jobs/dev-token", (
            [FromQuery] string? scopeId,
            [FromQuery] string? scopeKind,
            [FromServices] IHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
            {
                return Results.NotFound();
            }

            var targetScope = string.IsNullOrWhiteSpace(scopeId) ? "school-hn-01" : scopeId.Trim();
            JobScopeKind? targetKind = null;
            if (!string.IsNullOrWhiteSpace(scopeKind) &&
                Enum.TryParse<JobScopeKind>(scopeKind, ignoreCase: true, out var parsedKind) &&
                Enum.IsDefined(parsedKind))
            {
                targetKind = parsedKind;
            }

            var devEntry = JobEngineAuthenticationHandler.DevelopmentTokens
                .FirstOrDefault(kvp => string.Equals(kvp.Value.ScopeId, targetScope, StringComparison.OrdinalIgnoreCase) &&
                                       (targetKind is null || kvp.Value.ScopeKind == targetKind.Value));

            if (string.IsNullOrEmpty(devEntry.Key))
            {
                devEntry = JobEngineAuthenticationHandler.DevelopmentTokens.First();
            }

            return Results.Ok(new
            {
                token = devEntry.Key,
                userId = devEntry.Value.UserId,
                scopeId = devEntry.Value.ScopeId,
                scopeKind = devEntry.Value.ScopeKind.ToString()
            });
        })
        .WithTags("JobsDev")
        .AllowAnonymous();

        return group;
    }

    /// <summary>
    /// Trích xuất và xác thực phạm vi phân quyền của người gọi từ danh tính đã xác thực (ClaimsPrincipal).
    /// Bao gồm cả ScopeKind và ScopeResourceId.
    /// Tuyệt đối không tin cậy các HTTP Headers do Client tự gửi để triệt tiêu lỗ hổng IDOR / Scope Bypass (CWE-639, CWE-862).
    /// </summary>
    /// <param name="httpContext">HTTP Context hiện tại.</param>
    /// <returns>Đối tượng JobScope hợp lệ từ claim danh tính đã xác thực, hoặc null nếu không tồn tại claim hợp lệ.</returns>
    internal static JobScope? ResolveAuthenticatedScope(HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var claimScopeId = httpContext.User.FindFirst("scope_id")?.Value
                ?? httpContext.User.FindFirst("school_id")?.Value
                ?? httpContext.User.FindFirst("scope")?.Value
                ?? httpContext.User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrWhiteSpace(claimScopeId))
            {
                return null;
            }

            var claimScopeKind = httpContext.User.FindFirst("scope_kind")?.Value;
            var kind = JobScopeKind.School;
            if (!string.IsNullOrWhiteSpace(claimScopeKind) &&
                Enum.TryParse<JobScopeKind>(claimScopeKind, ignoreCase: true, out var parsedKind) &&
                Enum.IsDefined(parsedKind))
            {
                kind = parsedKind;
            }

            return JobScope.Create(kind, claimScopeId.Trim());
        }

        return null;
    }
}
