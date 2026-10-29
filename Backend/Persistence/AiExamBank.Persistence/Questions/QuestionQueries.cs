using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

internal sealed class QuestionQueries(AiExamBankDbContext db) : IQuestionQueries
{
    private readonly QuestionAccess _access = new(db);

    public async Task<PagedResult<QuestionListItem>> SearchAsync(
        QuestionSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var assignments = await _access.LoadAssignmentsAsync(query.ActorUserId, query.SchoolId, cancellationToken);
        var teacherDepartments = QuestionAccess.DepartmentsOf(assignments, AppRole.Teacher);
        var headDepartments = QuestionAccess.DepartmentsOf(assignments, AppRole.DepartmentHead);
        if (teacherDepartments.Count == 0 && headDepartments.Count == 0)
            return new PagedResult<QuestionListItem>([], query.Page, query.PageSize, 0);

        var rows = (
            from question in db.Questions.AsNoTracking()
            join revision in db.QuestionRevisions.AsNoTracking()
                on new { question.Id, Revision = question.LatestRevision }
                equals new { Id = revision.QuestionId, revision.Revision }
            join subject in db.Subjects.AsNoTracking() on question.SubjectId equals subject.Id
            join topic in db.Topics.AsNoTracking() on revision.TopicId equals topic.Id
            where question.SchoolId == query.SchoolId
            select new QuestionRow
            {
                QuestionId = question.Id,
                Revision = revision.Revision,
                SchoolId = question.SchoolId,
                DepartmentId = question.DepartmentId,
                OwnerUserId = question.OwnerUserId,
                SubjectId = question.SubjectId,
                SubjectName = subject.Name,
                TopicId = topic.Id,
                TopicName = topic.Name,
                Difficulty = revision.Difficulty,
                LatestStatus = revision.Status,
                Stem = revision.Stem,
                UpdatedAtUtc = question.UpdatedAtUtc
            })
            .Where(QuestionAccess.VisibleTo(query.ActorUserId, teacherDepartments, headDepartments));

        if (query.SubjectId is Guid subjectId) rows = rows.Where(row => row.SubjectId == subjectId);
        if (query.TopicId is Guid topicId) rows = rows.Where(row => row.TopicId == topicId);
        if (query.Difficulty is Difficulty difficulty) rows = rows.Where(row => row.Difficulty == difficulty);
        if (query.Status is QuestionStatus status) rows = rows.Where(row => row.LatestStatus == status);
        if (query.Text is string text)
        {
            rows = Guid.TryParse(text, out var questionId)
                ? rows.Where(row => row.QuestionId == questionId || row.Stem.Contains(text))
                : rows.Where(row => row.Stem.Contains(text));
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(row => row.UpdatedAtUtc)
            .ThenBy(row => row.QuestionId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(row => new QuestionListItem(
            row.QuestionId, row.Revision, row.Stem, row.SubjectId, row.SubjectName, row.TopicId, row.TopicName,
            row.Difficulty, row.LatestStatus, row.OwnerUserId, row.UpdatedAtUtc)).ToArray();
        return new PagedResult<QuestionListItem>(items, query.Page, query.PageSize, total);
    }

    public async Task<QuestionDetail?> GetAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await db.Questions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == questionId, cancellationToken);
        if (question is null) return null;

        var revisions = await db.QuestionRevisions.AsNoTracking()
            .Where(item => item.QuestionId == questionId)
            .OrderByDescending(item => item.Revision)
            .ToListAsync(cancellationToken);
        var latest = revisions.FirstOrDefault(item => item.Revision == question.LatestRevision);
        if (latest is null) return null;

        var assignments = await _access.LoadAssignmentsAsync(actorUserId, question.SchoolId, cancellationToken);
        var resource = QuestionAccess.ResourceOf(question.SchoolId, question.DepartmentId, question.OwnerUserId, latest.Status);
        if (!ResourceAccessPolicy.CanAccess(actorUserId, resource, ResourceAction.Read, assignments)) return null;

        var options = await db.QuestionOptions.AsNoTracking()
            .Where(item => item.QuestionId == questionId && item.Revision == latest.Revision)
            .OrderBy(item => item.Label)
            .Select(item => item.Text)
            .ToListAsync(cancellationToken);
        var subjectName = await db.Subjects.AsNoTracking()
            .Where(item => item.Id == question.SubjectId).Select(item => item.Name).SingleAsync(cancellationToken);
        var topicName = await db.Topics.AsNoTracking()
            .Where(item => item.Id == latest.TopicId).Select(item => item.Name).SingleAsync(cancellationToken);

        return new QuestionDetail(
            question.Id, latest.Revision, latest.Status, latest.Origin, latest.Stem, options,
            latest.CorrectOption[0], question.SubjectId, subjectName, latest.TopicId, topicName,
            latest.Difficulty, latest.SourceText, question.OwnerUserId, question.UpdatedAtUtc,
            revisions.Select(item => new QuestionRevisionSummary(
                item.Revision, item.Status, item.Origin, item.CreatedByUserId, item.CreatedAtUtc)).ToArray());
    }
}
