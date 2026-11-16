using AiExamBank.Modules.Questions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AiExamBank.Persistence.Questions;

/// <summary>
/// Refuses, before anything reaches the database, every write that would rewrite question history. A revision's
/// content and options are written once; an edit is a new revision. Only a revision's status may change, and an
/// approved or rejected revision is final. The Review module moves a revision between the other statuses, so
/// that path is not restricted here.
/// </summary>
internal static class QuestionImmutabilityGuard
{
    /// <summary>The columns the Review module (M3-03) moves on a revision: not content, so not covered by immutability.</summary>
    private static readonly HashSet<string> ReviewBookkeeping =
    [
        nameof(QuestionRevisionEntity.Status),
        nameof(QuestionRevisionEntity.ReviewVersion),
        nameof(QuestionRevisionEntity.SubmittedAtUtc)
    ];

    public static void Enforce(ChangeTracker tracker)
    {
        foreach (var entry in tracker.Entries())
        {
            switch (entry.Entity)
            {
                case QuestionRevisionEntity:
                    CheckRevision(entry);
                    break;
                case QuestionOptionEntity:
                    if (entry.State is EntityState.Modified or EntityState.Deleted)
                        throw new QuestionImmutableException("The options of a revision cannot be changed or deleted.");
                    break;
                case QuestionEntity:
                    CheckQuestion(entry);
                    break;
            }
        }
    }

    private static void CheckRevision(EntityEntry entry)
    {
        if (entry.State == EntityState.Deleted)
            throw new QuestionImmutableException("A revision cannot be deleted.");
        if (entry.State != EntityState.Modified) return;

        var changed = entry.Properties.Where(item => item.IsModified && Changed(item)).ToList();
        foreach (var property in changed.Where(item => !ReviewBookkeeping.Contains(item.Metadata.Name)))
            throw new QuestionImmutableException(
                $"The {property.Metadata.Name} of a revision cannot be changed; save a new revision instead.");

        // Whatever the review flow touches (status, version token, submit time) is frozen once the revision is decided.
        if (changed.Count == 0) return;
        var from = (QuestionStatus)entry.Property(nameof(QuestionRevisionEntity.Status)).OriginalValue!;
        if (from is QuestionStatus.Approved or QuestionStatus.Rejected)
            throw new QuestionImmutableException(
                $"A revision that is {from} is final; save a new revision instead.");
    }

    private static void CheckQuestion(EntityEntry entry)
    {
        if (entry.State == EntityState.Deleted)
            throw new QuestionImmutableException("A question cannot be deleted.");
        if (entry.State != EntityState.Modified) return;

        var latest = entry.Property(nameof(QuestionEntity.LatestRevision));
        if (latest.IsModified && (int)latest.CurrentValue! < (int)latest.OriginalValue!)
            throw new QuestionImmutableException("The latest revision of a question can only move forward.");
    }

    private static bool Changed(PropertyEntry property) => !Equals(property.OriginalValue, property.CurrentValue);
}
