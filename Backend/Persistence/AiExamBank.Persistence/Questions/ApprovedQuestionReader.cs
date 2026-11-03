using AiExamBank.Modules.Questions.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

/// <summary>
/// Exam selection reads only APPROVED revisions. For a question edited after approval, the latest approved
/// revision is the one in use, even while a newer draft exists.
/// </summary>
internal sealed class ApprovedQuestionReader(AiExamBankDbContext db) : IApprovedQuestionReader
{
    public async Task<IReadOnlyList<ApprovedQuestionRead>> FindApprovedAsync(
        ApprovedQuestionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var excluded = query.ExcludedQuestionIds.ToArray();

        var revisions = await (
            from revision in db.QuestionRevisions.AsNoTracking()
            join question in db.Questions.AsNoTracking() on revision.QuestionId equals question.Id
            where question.SchoolId == query.SchoolId &&
                  revision.Status == QuestionStatus.Approved &&
                  revision.TopicId == query.TopicId &&
                  revision.Difficulty == query.Difficulty &&
                  !excluded.Contains(question.Id) &&
                  !db.QuestionRevisions.Any(newer =>
                      newer.QuestionId == revision.QuestionId &&
                      newer.Revision > revision.Revision &&
                      newer.Status == QuestionStatus.Approved)
            orderby question.Id
            select new { Revision = revision, question.DepartmentId })
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        return await ToReadsAsync(
            revisions.Select(item => (item.Revision, query.SchoolId, item.DepartmentId)).ToArray(), cancellationToken);
    }

    public async Task<ApprovedQuestionRead?> GetApprovedAsync(
        QuestionRevisionRef revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var found = await (
            from item in db.QuestionRevisions.AsNoTracking()
            join question in db.Questions.AsNoTracking() on item.QuestionId equals question.Id
            where item.QuestionId == revision.QuestionId && item.Revision == revision.Revision &&
                  item.Status == QuestionStatus.Approved
            select new { Revision = item, question.SchoolId, question.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null) return null;

        var reads = await ToReadsAsync([(found.Revision, found.SchoolId, found.DepartmentId)], cancellationToken);
        return reads[0];
    }

    public async Task<IReadOnlyList<ApprovedQuestionCount>> CountApprovedAsync(
        Guid schoolId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var counts = await (
            from revision in db.QuestionRevisions.AsNoTracking()
            join question in db.Questions.AsNoTracking() on revision.QuestionId equals question.Id
            where question.SchoolId == schoolId && question.SubjectId == subjectId &&
                  revision.Status == QuestionStatus.Approved &&
                  !db.QuestionRevisions.Any(newer =>
                      newer.QuestionId == revision.QuestionId &&
                      newer.Revision > revision.Revision &&
                      newer.Status == QuestionStatus.Approved)
            group revision by new { revision.TopicId, revision.Difficulty } into cell
            select new { cell.Key.TopicId, cell.Key.Difficulty, Count = cell.Count() })
            .ToListAsync(cancellationToken);

        return counts
            .OrderBy(cell => cell.TopicId).ThenBy(cell => cell.Difficulty)
            .Select(cell => new ApprovedQuestionCount(cell.TopicId, cell.Difficulty, cell.Count))
            .ToArray();
    }

    private async Task<IReadOnlyList<ApprovedQuestionRead>> ToReadsAsync(
        IReadOnlyList<(QuestionRevisionEntity Revision, Guid SchoolId, Guid DepartmentId)> revisions,
        CancellationToken cancellationToken)
    {
        if (revisions.Count == 0) return [];

        var questionIds = revisions.Select(item => item.Revision.QuestionId).Distinct().ToArray();
        var revisionNumbers = revisions.Select(item => item.Revision.Revision).Distinct().ToArray();
        // A question edited many times has many option rows; only the pinned revisions are wanted.
        var options = (await db.QuestionOptions.AsNoTracking()
                .Where(option => questionIds.Contains(option.QuestionId) && revisionNumbers.Contains(option.Revision))
                .ToListAsync(cancellationToken))
            .ToLookup(option => (option.QuestionId, option.Revision));

        return revisions.Select(item => new ApprovedQuestionRead(
            QuestionRevisionRef.Create(item.Revision.QuestionId, item.Revision.Revision),
            item.SchoolId,
            item.DepartmentId,
            item.Revision.SubjectId,
            item.Revision.TopicId,
            item.Revision.Difficulty,
            item.Revision.Stem,
            options[(item.Revision.QuestionId, item.Revision.Revision)]
                .OrderBy(option => option.Label)
                .Select(option => option.Text)
                .ToArray(),
            item.Revision.CorrectOption[0])).ToArray();
    }
}
