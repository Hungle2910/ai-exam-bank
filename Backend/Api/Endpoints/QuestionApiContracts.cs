using AiExamBank.Modules.Questions.Contracts;

namespace AiExamBank.Api.Endpoints;

// The JSON bodies of the question bank API. The handlers build these records, so the OpenAPI document
// generated from the endpoints and the bytes on the wire cannot drift apart. Enum values travel as the
// upper-case names produced by ApiNames.

internal sealed record NamedRef(Guid Id, string Name);

internal sealed record CatalogTopicResponse(Guid Id, string Code, string Name);

internal sealed record CatalogSubjectResponse(
    Guid Id, string Code, string Name, bool CanWrite, IReadOnlyList<CatalogTopicResponse> Topics);

internal sealed record QuestionSummaryResponse(
    Guid Id,
    int Revision,
    string Status,
    string Stem,
    NamedRef Subject,
    NamedRef Topic,
    string Difficulty,
    Guid OwnerUserId,
    DateTimeOffset UpdatedAt);

internal sealed record QuestionPageResponse(
    IReadOnlyList<QuestionSummaryResponse> Items, int Page, int PageSize, int Total);

internal sealed record QuestionRevisionResponse(
    int Revision, string Status, string Origin, Guid CreatedByUserId, DateTimeOffset CreatedAt);

internal sealed record QuestionDetailResponse(
    Guid Id,
    int Revision,
    string Status,
    string Origin,
    string Stem,
    IReadOnlyList<string> Options,
    string CorrectOption,
    NamedRef Subject,
    NamedRef Topic,
    string Difficulty,
    string? Source,
    Guid OwnerUserId,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuestionRevisionResponse> Revisions,
    int? ApprovedRevision)
{
    public static QuestionDetailResponse From(QuestionDetail detail) => new(
        detail.QuestionId,
        detail.Revision,
        ApiNames.ToApi(detail.Status),
        ApiNames.ToApi(detail.Origin),
        detail.Stem,
        detail.Options,
        detail.CorrectOption.ToString(),
        new NamedRef(detail.SubjectId, detail.SubjectName),
        new NamedRef(detail.TopicId, detail.TopicName),
        ApiNames.ToApi(detail.Difficulty),
        detail.SourceText,
        detail.OwnerUserId,
        detail.UpdatedAtUtc,
        detail.Revisions.Select(revision => new QuestionRevisionResponse(
            revision.Revision,
            ApiNames.ToApi(revision.Status),
            ApiNames.ToApi(revision.Origin),
            revision.CreatedByUserId,
            revision.CreatedAtUtc)).ToList(),
        detail.ApprovedRevision);
}

internal sealed record QuestionFieldError(string Code, string Field, string Message);

/// <summary>RFC 9457 Problem Details plus the stable <c>code</c> and the <c>traceId</c> every business error carries.</summary>
internal sealed record ApiProblem(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    string Code,
    string TraceId,
    IReadOnlyList<QuestionFieldError>? Errors);

internal sealed record SchoolSubjectResponse(Guid SubjectId, string Code, string Name, Guid DepartmentId);

internal sealed record SchoolSubjectMappingResponse(Guid SubjectId, Guid DepartmentId);

internal sealed record QuestionRevisionDetailResponse(
    int Revision,
    string Status,
    string Origin,
    string Stem,
    IReadOnlyList<string> Options,
    string CorrectOption,
    NamedRef Topic,
    string Difficulty,
    string? Source,
    Guid? ImportBatchId,
    int? ImportRowNumber,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    bool IsLatest,
    bool IsApprovedInUse)
{
    public static QuestionRevisionDetailResponse From(QuestionRevisionDetail revision) => new(
        revision.Revision,
        ApiNames.ToApi(revision.Status),
        ApiNames.ToApi(revision.Origin),
        revision.Stem,
        revision.Options,
        revision.CorrectOption.ToString(),
        new NamedRef(revision.TopicId, revision.TopicName),
        ApiNames.ToApi(revision.Difficulty),
        revision.SourceText,
        revision.ImportBatchId,
        revision.ImportRowNumber,
        revision.CreatedByUserId,
        revision.CreatedAtUtc,
        revision.IsLatest,
        revision.IsApprovedInUse);
}

internal sealed record QuestionHistoryResponse(
    Guid Id,
    NamedRef Subject,
    int LatestRevision,
    int? ApprovedRevision,
    IReadOnlyList<QuestionRevisionDetailResponse> Revisions)
{
    public static QuestionHistoryResponse From(QuestionHistory history) => new(
        history.QuestionId,
        new NamedRef(history.SubjectId, history.SubjectName),
        history.LatestRevision,
        history.ApprovedRevision,
        history.Revisions.Select(QuestionRevisionDetailResponse.From).ToList());
}
