using AiExamBank.Modules.Identity.Authorization;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Review.Contracts;

namespace AiExamBank.Modules.Review.Domain;

// ReviewRequired is the pending state in the M4 question contract.
public enum RevisionReviewState { Draft, ReviewRequired, Approved, Rejected }

public sealed record ReviewRevisionSnapshot(
    QuestionRevisionRef Revision,
    Guid SchoolId,
    Guid DepartmentId,
    Guid OwnerUserId,
    Guid RevisionAuthorUserId,
    RevisionReviewState State,
    long Version);

public enum ReviewTransitionFailure { InvalidRequest, Forbidden, Conflict }

public sealed record ReviewTransitionResult(
    ReviewRevisionSnapshot? Updated,
    ReviewTransitionFailure? Failure)
{
    public bool Succeeded => Updated is not null;
}

/// <summary>
/// Pure transition rules. The API must load the snapshot and current assignments from the database,
/// then persist the returned transition and audit record atomically with a version predicate.
/// </summary>
public static class ReviewWorkflow
{
    public static ReviewTransitionResult Submit(
        ReviewRevisionSnapshot snapshot,
        Guid actorUserId,
        long expectedVersion,
        IReadOnlyCollection<RoleAssignment> currentAssignments)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(currentAssignments);

        if (actorUserId == Guid.Empty || expectedVersion < 1)
            return Failed(ReviewTransitionFailure.InvalidRequest);
        if (!ResourceAccessPolicy.CanAccess(actorUserId, Resource(snapshot, snapshot.OwnerUserId, false),
                ResourceAction.Edit, currentAssignments))
            return Failed(ReviewTransitionFailure.Forbidden);
        if (snapshot.State != RevisionReviewState.Draft ||
            snapshot.Version != expectedVersion || snapshot.Version == long.MaxValue)
            return Failed(ReviewTransitionFailure.Conflict);

        return Succeeded(snapshot with
        {
            State = RevisionReviewState.ReviewRequired,
            Version = snapshot.Version + 1
        });
    }

    public static ReviewTransitionResult Decide(
        ReviewRevisionSnapshot snapshot,
        ReviewDecisionRequest request,
        IReadOnlyCollection<RoleAssignment> currentAssignments)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentAssignments);

        if (request.Revision is null || request.ReviewerId == Guid.Empty ||
            request.ExpectedVersion < 1 || !Enum.IsDefined(request.Decision) ||
            (request.Decision == ReviewDecision.Reject && string.IsNullOrWhiteSpace(request.Reason)))
            return Failed(ReviewTransitionFailure.InvalidRequest);

        // Check reviewer scope and self-review independently of the current state.
        // A scoped reviewer gets Conflict for a revision already decided by someone else.
        if (request.ReviewerId == snapshot.OwnerUserId ||
            !ResourceAccessPolicy.CanAccess(request.ReviewerId,
                Resource(snapshot, snapshot.RevisionAuthorUserId, true),
                ResourceAction.Review, currentAssignments))
            return Failed(ReviewTransitionFailure.Forbidden);
        if (snapshot.Revision != request.Revision ||
            snapshot.State != RevisionReviewState.ReviewRequired ||
            snapshot.Version != request.ExpectedVersion || snapshot.Version == long.MaxValue)
            return Failed(ReviewTransitionFailure.Conflict);

        return Succeeded(snapshot with
        {
            State = request.Decision == ReviewDecision.Approve
                ? RevisionReviewState.Approved
                : RevisionReviewState.Rejected,
            Version = snapshot.Version + 1
        });
    }

    private static SchoolResource Resource(
        ReviewRevisionSnapshot snapshot, Guid ownerUserId, bool submitted) =>
        new(ResourceKind.Question, snapshot.SchoolId, snapshot.DepartmentId,
            ownerUserId, submitted);

    private static ReviewTransitionResult Failed(ReviewTransitionFailure failure) => new(null, failure);
    private static ReviewTransitionResult Succeeded(ReviewRevisionSnapshot snapshot) => new(snapshot, null);
}
