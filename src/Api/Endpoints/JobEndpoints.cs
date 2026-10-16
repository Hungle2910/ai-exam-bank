using System.Diagnostics;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using Microsoft.AspNetCore.Mvc;

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
/// DTO phản hồi trạng thái an toàn của tác vụ ngầm cho Client và Status UI.
/// </summary>
public sealed record JobApiResponse(
    Guid JobId,
    string Status,
    int? ProgressPercentage,
    string? ResultId,
    string? ErrorCode,
    string? SafeMessage);

/// <summary>
/// Định nghĩa các HTTP Endpoints quản lý và tra cứu tác vụ ngầm thuộc chuẩn M5-02.
/// </summary>
public static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/jobs")
            .WithTags("Jobs");

        // 1. POST /api/v1/jobs - Đẩy tác vụ mới vào hàng đợi (Idempotent Enqueue)
        group.MapPost("/", async (
            [FromBody] CreateJobApiRequest request,
            [FromHeader(Name = "X-User-Id")] string? headerUserId,
            [FromHeader(Name = "X-Correlation-Id")] string? headerCorrelationId,
            [FromServices] IJobPublisher publisher,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var correlationId = string.IsNullOrWhiteSpace(headerCorrelationId)
                ? Activity.Current?.Id ?? httpContext.TraceIdentifier
                : headerCorrelationId;

            var requesterId = string.IsNullOrWhiteSpace(headerUserId)
                ? "system-user"
                : headerUserId;

            if (!Enum.TryParse<JobScopeKind>(request.ScopeKind, ignoreCase: true, out var scopeKind))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Kind",
                    detail: $"Scope kind '{request.ScopeKind}' is invalid. Supported values: School, MinistryExam.");
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
            [FromHeader(Name = "X-User-Id")] string? headerUserId,
            [FromHeader(Name = "X-Scope-Id")] string? headerScopeId,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = string.IsNullOrWhiteSpace(headerUserId) ? "system-user" : headerUserId;
            var jobRef = JobRef.Create(id);

            try
            {
                var status = await tracker.GetJobStatusAsync(jobRef, requesterId, headerScopeId, cancellationToken);

                return Results.Ok(new JobApiResponse(
                    status.Job.Value,
                    status.Status.ToString(),
                    status.ProgressPercentage,
                    status.ResultId,
                    status.ErrorCode,
                    status.SafeMessage));
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
            [FromHeader(Name = "X-User-Id")] string? headerUserId,
            [FromServices] IJobTracker tracker,
            CancellationToken cancellationToken) =>
        {
            var requesterId = string.IsNullOrWhiteSpace(headerUserId) ? "system-user" : headerUserId;

            if (!Enum.TryParse<JobScopeKind>(scopeKind, ignoreCase: true, out var parsedKind))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Scope Kind",
                    detail: $"Scope kind '{scopeKind}' is invalid. Supported values: School, MinistryExam.");
            }

            JobStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }

            var scope = JobScope.Create(parsedKind, scopeResourceId);
            var jobs = await tracker.ListJobsAsync(scope, requesterId, statusFilter, cancellationToken);

            var response = jobs.Select(j => new JobApiResponse(
                j.Job.Value,
                j.Status.ToString(),
                j.ProgressPercentage,
                j.ResultId,
                j.ErrorCode,
                j.SafeMessage)).ToList();

            return Results.Ok(response);
        });

        // 4. POST /api/v1/jobs/{id}/cancel - Yêu cầu hủy tác vụ an toàn
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromHeader(Name = "X-User-Id")] string? headerUserId,
            [FromHeader(Name = "X-Scope-Id")] string? headerScopeId,
            [FromServices] IJobTracker tracker,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requesterId = string.IsNullOrWhiteSpace(headerUserId) ? "system-user" : headerUserId;
            var jobRef = JobRef.Create(id);

            try
            {
                var cancelled = await tracker.CancelJobAsync(jobRef, requesterId, headerScopeId, cancellationToken);

                if (!cancelled)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Cancellation Rejected",
                        detail: "The job cannot be cancelled because it has already completed, failed, or does not exist.",
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

        return group;
    }
}
