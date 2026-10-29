using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

/// <summary>
/// Writes DRAFT revisions through the caller's <see cref="AiExamBankDbContext"/>, so a surrounding transaction
/// (an import commit, for example) covers these writes. Nothing here approves or publishes content.
/// </summary>
internal sealed class QuestionWriter(AiExamBankDbContext db, TimeProvider timeProvider) : IQuestionWriter
{
    private readonly QuestionAccess _access = new(db);

    public async Task<QuestionWriteResult> CreateDraftAsync(
        CreateQuestionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = command.Content;
        var validation = McqContent.Validate(input);
        var errors = validation.Errors.ToList();

        var mapping = input.SubjectId == Guid.Empty
            ? null
            : await db.SchoolSubjects.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchoolId == command.SchoolId && item.SubjectId == input.SubjectId, cancellationToken);

        if (mapping is null)
        {
            if (input.SubjectId != Guid.Empty)
                errors.Add(new(QuestionErrorCodes.SubjectNotFound, "subjectId", "The school does not offer this subject."));
        }
        else
        {
            var assignments = await _access.LoadAssignmentsAsync(command.ActorUserId, command.SchoolId, cancellationToken);
            var resource = QuestionAccess.ResourceOf(command.SchoolId, mapping.DepartmentId, command.ActorUserId, QuestionStatus.Draft);
            if (!ResourceAccessPolicy.CanAccess(command.ActorUserId, resource, ResourceAction.Edit, assignments))
                throw new QuestionAccessDeniedException("The actor may not write questions for this subject.");

            await AddTopicErrorAsync(errors, input, input.SubjectId, cancellationToken);
        }

        if (errors.Count > 0) throw new QuestionValidationException(errors);
        await ThrowIfImportRowWrittenAsync(command.Provenance, cancellationToken);

        var content = validation.Content!;
        var now = timeProvider.GetUtcNow();
        var question = new QuestionEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = command.SchoolId,
            DepartmentId = mapping!.DepartmentId,
            SubjectId = content.SubjectId,
            OwnerUserId = command.ActorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LatestRevision = 1
        };

        db.Questions.Add(question);
        var touched = new List<object> { question };
        touched.AddRange(AddRevision(question.Id, 1, content, command.Provenance, command.ActorUserId, now));
        await SaveAsync(touched, question.Id, 1, command.Provenance, cancellationToken);

        return new QuestionWriteResult(QuestionRevisionRef.Create(question.Id, 1), QuestionStatus.Draft);
    }

    public async Task<QuestionWriteResult> CreateRevisionAsync(
        CreateRevisionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var question = await db.Questions.SingleOrDefaultAsync(item => item.Id == command.QuestionId, cancellationToken)
            ?? throw new QuestionNotFoundException("The question was not found.");
        var latestStatus = await db.QuestionRevisions.AsNoTracking()
            .Where(revision => revision.QuestionId == question.Id && revision.Revision == question.LatestRevision)
            .Select(revision => revision.Status)
            .SingleAsync(cancellationToken);

        // A question the actor may not read is reported as missing, so its existence is not revealed.
        var assignments = await _access.LoadAssignmentsAsync(command.ActorUserId, question.SchoolId, cancellationToken);
        var resource = QuestionAccess.ResourceOf(question.SchoolId, question.DepartmentId, question.OwnerUserId, latestStatus);
        if (!ResourceAccessPolicy.CanAccess(command.ActorUserId, resource, ResourceAction.Read, assignments))
            throw new QuestionNotFoundException("The question was not found.");
        if (!ResourceAccessPolicy.CanAccess(command.ActorUserId, resource, ResourceAction.Edit, assignments))
            throw new QuestionAccessDeniedException("The actor may not edit this question.");

        // A newer draft would pull the question out of the reviewer's queue while it is being decided.
        if (latestStatus == QuestionStatus.ReviewRequired)
            throw new QuestionConflictException("The question is waiting for review and cannot be edited until it is decided.");

        if (question.LatestRevision != command.BaseRevision)
            throw new QuestionConflictException(
                $"The question is at revision {question.LatestRevision}, not revision {command.BaseRevision}.");

        var input = command.Content;
        var validation = McqContent.Validate(input);
        var errors = validation.Errors.ToList();
        if (input.SubjectId != Guid.Empty && input.SubjectId != question.SubjectId)
            errors.Add(new(QuestionErrorCodes.SubjectImmutable, "subjectId", "The subject of a question cannot change."));
        else
            await AddTopicErrorAsync(errors, input, question.SubjectId, cancellationToken);

        if (errors.Count > 0) throw new QuestionValidationException(errors);
        await ThrowIfImportRowWrittenAsync(command.Provenance, cancellationToken);

        var next = question.LatestRevision + 1;
        var now = timeProvider.GetUtcNow();
        var touched = new List<object> { question };
        touched.AddRange(AddRevision(question.Id, next, validation.Content!, command.Provenance, command.ActorUserId, now));
        question.LatestRevision = next;
        question.UpdatedAtUtc = now;
        await SaveAsync(touched, question.Id, next, command.Provenance, cancellationToken);

        return new QuestionWriteResult(QuestionRevisionRef.Create(question.Id, next), QuestionStatus.Draft);
    }

    private async Task AddTopicErrorAsync(
        List<QuestionValidationError> errors, McqInput input, Guid subjectId, CancellationToken cancellationToken)
    {
        // A topic that does not belong to the chosen subject is already reported by the content rules.
        if (input.Topic is null || input.Topic.SubjectId != subjectId) return;

        var exists = await db.Topics.AsNoTracking().AnyAsync(
            topic => topic.Id == input.Topic.TopicId && topic.SubjectId == subjectId, cancellationToken);
        if (!exists)
            errors.Add(new(QuestionErrorCodes.TopicNotFound, "topicId", "The topic does not exist for this subject."));
    }

    private async Task ThrowIfImportRowWrittenAsync(QuestionProvenance provenance, CancellationToken cancellationToken)
    {
        if (await ImportRowWrittenAsync(provenance, cancellationToken))
            throw new QuestionConflictException("This import row has already been written.");
    }

    private async Task<bool> ImportRowWrittenAsync(QuestionProvenance provenance, CancellationToken cancellationToken) =>
        provenance.ImportBatchId is Guid batchId &&
        await db.QuestionRevisions.AsNoTracking().AnyAsync(
            revision => revision.ImportBatchId == batchId && revision.ImportRowNumber == provenance.ImportRowNumber,
            cancellationToken);

    private IEnumerable<object> AddRevision(
        Guid questionId, int revision, McqContent content, QuestionProvenance provenance, Guid actorUserId, DateTimeOffset now)
    {
        var added = new List<object>();
        var entity = new QuestionRevisionEntity
        {
            QuestionId = questionId,
            Revision = revision,
            SubjectId = content.SubjectId,
            TopicId = content.TopicId,
            Difficulty = content.Difficulty,
            Stem = content.Stem,
            CorrectOption = content.CorrectOption.ToString(),
            Status = QuestionStatus.Draft,
            Origin = provenance.Origin,
            SourceText = content.SourceText,
            ImportBatchId = provenance.ImportBatchId,
            ImportRowNumber = provenance.ImportRowNumber,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now
        };
        db.QuestionRevisions.Add(entity);
        added.Add(entity);

        for (var index = 0; index < content.Options.Count; index++)
        {
            var option = new QuestionOptionEntity
            {
                QuestionId = questionId,
                Revision = revision,
                Label = QuestionRules.OptionLabels[index].ToString(),
                Text = content.Options[index]
            };
            db.QuestionOptions.Add(option);
            added.Add(option);
        }

        return added;
    }

    /// <summary>
    /// Saves one write. The context is shared with the caller, which may carry on after a failure (an import
    /// moving to its next row), so on any failure the entities this call touched are detached rather than left
    /// to be saved again by the next call.
    /// </summary>
    private async Task SaveAsync(
        IReadOnlyList<object> touched, Guid questionId, int revision, QuestionProvenance provenance,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            Detach(touched);
            throw new QuestionConflictException("The question was changed by someone else. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            Detach(touched);

            // Two editors racing for the same revision number: the loser hits the primary key.
            var taken = await db.QuestionRevisions.AsNoTracking().AnyAsync(
                item => item.QuestionId == questionId && item.Revision == revision, cancellationToken);
            if (taken)
                throw new QuestionConflictException("The question was changed by someone else. Reload and try again.");

            // Two retries of the same import row: the loser hits the unique index on (batch, row).
            if (await ImportRowWrittenAsync(provenance, cancellationToken))
                throw new QuestionConflictException("This import row has already been written.");
            throw;
        }
        catch
        {
            Detach(touched);
            throw;
        }
    }

    private void Detach(IEnumerable<object> entities)
    {
        foreach (var entity in entities)
            db.Entry(entity).State = EntityState.Detached;
    }
}
