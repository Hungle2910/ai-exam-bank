using System.Globalization;
using System.Text;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

internal sealed class QuestionCatalog(AiExamBankDbContext db) : IQuestionCatalog
{
    // A fixed Vietnamese order, so the dropdowns do not depend on the server's current culture. A host running
    // with invariant globalization has no vi-VN culture and falls back to ordinal order.
    private static readonly StringComparer NameOrder = CreateNameOrder();

    /// <summary>Teachers and department heads see their departments' subjects, so both can filter the bank.</summary>
    public Task<IReadOnlyList<CatalogSubject>> ListAsync(
        Guid actorUserId, Guid schoolId, CancellationToken cancellationToken = default) =>
        LoadAsync(actorUserId, schoolId, includeDepartmentHeads: true, cancellationToken);

    /// <summary>Only subjects the actor can write to resolve, so an import cannot name another department's subject.</summary>
    public async Task<CatalogResolution> ResolveAsync(
        Guid actorUserId, Guid schoolId, string? subjectName, string? topicName, CancellationToken cancellationToken = default)
    {
        var errors = new List<QuestionValidationError>();
        var subjectKey = Normalize(subjectName);
        var topicKey = Normalize(topicName);
        if (subjectKey.Length == 0)
            errors.Add(new(QuestionErrorCodes.SubjectRequired, "subject", "The subject is required."));
        if (topicKey.Length == 0)
            errors.Add(new(QuestionErrorCodes.TopicRequired, "topic", "The topic is required."));
        if (errors.Count > 0) return new CatalogResolution(null, null, errors);

        var catalog = await LoadAsync(actorUserId, schoolId, includeDepartmentHeads: false, cancellationToken);
        var subject = catalog.FirstOrDefault(item => item.CanWrite && Same(item.Name, subjectKey));
        if (subject is null)
        {
            errors.Add(new(QuestionErrorCodes.SubjectNotFound, "subject", "The subject is not in the catalog for this school."));
            return new CatalogResolution(null, null, errors);
        }

        var topic = subject.Topics.FirstOrDefault(item => Same(item.Name, topicKey));
        if (topic is null)
        {
            errors.Add(new(QuestionErrorCodes.TopicNotFound, "topic", "The topic is not in the catalog for this subject."));
            return new CatalogResolution(subject.Id, null, errors);
        }

        return new CatalogResolution(subject.Id, QuestionTopic.Create(topic.Id, subject.Id), errors);
    }

    private async Task<IReadOnlyList<CatalogSubject>> LoadAsync(
        Guid actorUserId, Guid schoolId, bool includeDepartmentHeads, CancellationToken cancellationToken)
    {
        var roles = await db.RoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == actorUserId && assignment.SchoolId == schoolId &&
                assignment.DepartmentId != null &&
                (assignment.Role == AppRole.Teacher ||
                 (includeDepartmentHeads && assignment.Role == AppRole.DepartmentHead)))
            .Select(assignment => new { assignment.Role, DepartmentId = assignment.DepartmentId!.Value })
            .ToListAsync(cancellationToken);
        if (roles.Count == 0) return [];

        var departmentIds = roles.Select(item => item.DepartmentId).Distinct().ToArray();
        var writableDepartments = roles.Where(item => item.Role == AppRole.Teacher)
            .Select(item => item.DepartmentId).ToHashSet();

        var subjects = await (
            from mapping in db.SchoolSubjects.AsNoTracking()
            join subject in db.Subjects.AsNoTracking() on mapping.SubjectId equals subject.Id
            where mapping.SchoolId == schoolId && departmentIds.Contains(mapping.DepartmentId)
            select new { subject.Id, subject.Code, subject.Name, mapping.DepartmentId })
            .ToListAsync(cancellationToken);

        var subjectIds = subjects.Select(item => item.Id).ToArray();
        var topics = await db.Topics.AsNoTracking()
            .Where(topic => subjectIds.Contains(topic.SubjectId))
            .ToListAsync(cancellationToken);

        return subjects
            .OrderBy(item => item.Name, NameOrder)
            .Select(item => new CatalogSubject(
                item.Id, item.Code, item.Name, item.DepartmentId, writableDepartments.Contains(item.DepartmentId),
                topics.Where(topic => topic.SubjectId == item.Id)
                    .OrderBy(topic => topic.Name, NameOrder)
                    .Select(topic => new CatalogTopic(topic.Id, topic.Code, topic.Name))
                    .ToArray()))
            .ToArray();
    }

    private static StringComparer CreateNameOrder()
    {
        try
        {
            return StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);
        }
        catch (CultureNotFoundException)
        {
            return StringComparer.Ordinal;
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Normalize(NormalizationForm.FormC);

    private static bool Same(string catalogName, string key) =>
        string.Equals(Normalize(catalogName), key, StringComparison.OrdinalIgnoreCase);
}
