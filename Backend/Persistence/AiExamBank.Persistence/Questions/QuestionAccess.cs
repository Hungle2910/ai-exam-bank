using System.Linq.Expressions;
using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

/// <summary>A question joined with its latest revision, as the bank list reads and filters it.</summary>
internal sealed class QuestionRow
{
    public Guid QuestionId { get; init; }
    public int Revision { get; init; }
    public Guid SchoolId { get; init; }
    public Guid DepartmentId { get; init; }
    public Guid OwnerUserId { get; init; }
    public Guid SubjectId { get; init; }
    public string SubjectName { get; init; } = "";
    public Guid TopicId { get; init; }
    public string TopicName { get; init; } = "";
    public Difficulty Difficulty { get; init; }
    public QuestionStatus LatestStatus { get; init; }
    public string Stem { get; init; } = "";
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>
/// Loads current role assignments and applies <see cref="ResourceAccessPolicy"/> to questions.
/// <see cref="VisibleTo"/> is the same rule as the policy's Read action written as a query filter, so a list
/// can be filtered in the database; a test keeps the two in step.
/// </summary>
internal sealed class QuestionAccess(AiExamBankDbContext db)
{
    public async Task<IReadOnlyCollection<RoleAssignment>> LoadAssignmentsAsync(
        Guid actorUserId, Guid schoolId, CancellationToken cancellationToken)
    {
        var entities = await db.RoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == actorUserId && assignment.SchoolId == schoolId)
            .ToListAsync(cancellationToken);

        return entities.Select(assignment => RoleAssignment.Create(
            assignment.UserId, assignment.Role, assignment.SchoolId, assignment.DepartmentId)).ToArray();
    }

    public static SchoolResource ResourceOf(
        Guid schoolId, Guid departmentId, Guid ownerUserId, QuestionStatus latestStatus) =>
        new(ResourceKind.Question, schoolId, departmentId, ownerUserId, latestStatus == QuestionStatus.ReviewRequired);

    public static IReadOnlyList<Guid> DepartmentsOf(IEnumerable<RoleAssignment> assignments, AppRole role) =>
        assignments.Where(assignment => assignment.Role == role && assignment.DepartmentId is not null)
            .Select(assignment => assignment.DepartmentId!.Value)
            .Distinct()
            .ToArray();

    /// <summary>
    /// A teacher reads their own questions in their departments; a department head reads questions submitted
    /// for review in theirs. School scope is applied separately by the caller.
    /// </summary>
    public static Expression<Func<QuestionRow, bool>> VisibleTo(
        Guid actorUserId, IReadOnlyCollection<Guid> teacherDepartments, IReadOnlyCollection<Guid> headDepartments) =>
        row => (row.OwnerUserId == actorUserId && teacherDepartments.Contains(row.DepartmentId)) ||
               (headDepartments.Contains(row.DepartmentId) && row.LatestStatus == QuestionStatus.ReviewRequired);
}
