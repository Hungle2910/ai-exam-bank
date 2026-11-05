using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Review.Contracts;
using AiExamBank.Modules.Review.Domain;

namespace AiExamBank.Modules.Review.Tests;

[TestClass]
public sealed class ReviewWorkflowTests
{
    private readonly Guid school = Guid.NewGuid();
    private readonly Guid department = Guid.NewGuid();
    private readonly Guid author = Guid.NewGuid();
    private readonly Guid reviewer = Guid.NewGuid();

    [TestMethod]
    public void Teacher_submits_only_own_draft_in_assigned_department()
    {
        var draft = Snapshot(RevisionReviewState.Draft, 1);
        var teacher = RoleAssignment.Create(author, AppRole.Teacher, school, department);
        var submitted = ReviewWorkflow.Submit(draft, author, 1, [teacher]);

        Assert.IsTrue(submitted.Succeeded);
        Assert.AreEqual(RevisionReviewState.ReviewRequired, submitted.Updated!.State);
        Assert.AreEqual(2, submitted.Updated.Version);
        Assert.AreEqual(RevisionReviewState.Draft, draft.State);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Submit(draft, reviewer, 1, [teacher]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Submit(draft, author, 1, []).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Conflict,
            ReviewWorkflow.Submit(draft, author, 2, [teacher]).Failure);
    }

    [TestMethod]
    public void Scoped_department_head_can_approve_or_reject_a_submitted_revision()
    {
        var pending = Snapshot(RevisionReviewState.ReviewRequired, 2);
        var role = Head();

        var approved = ReviewWorkflow.Decide(pending,
            Decision(pending, reviewer, ReviewDecision.Approve, 2), [role]);
        var rejected = ReviewWorkflow.Decide(pending,
            Decision(pending, reviewer, ReviewDecision.Reject, 2, "Incorrect answer"), [role]);

        Assert.AreEqual(RevisionReviewState.Approved, approved.Updated!.State);
        Assert.AreEqual(RevisionReviewState.Rejected, rejected.Updated!.State);
        Assert.AreEqual(3, approved.Updated.Version);
        Assert.AreEqual(3, rejected.Updated.Version);
    }

    [TestMethod]
    public void Wrong_scope_and_self_review_are_forbidden()
    {
        var pending = Snapshot(RevisionReviewState.ReviewRequired, 2);
        var request = Decision(pending, reviewer, ReviewDecision.Approve, 2);

        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, request,
                [RoleAssignment.Create(reviewer, AppRole.DepartmentHead, school, Guid.NewGuid())]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, request,
                [RoleAssignment.Create(reviewer, AppRole.DepartmentHead, Guid.NewGuid(), department)]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, request,
                [RoleAssignment.Create(reviewer, AppRole.SchoolAdmin, school, null)]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, Decision(pending, author, ReviewDecision.Approve, 2),
                [RoleAssignment.Create(author, AppRole.DepartmentHead, school, department)]).Failure);
    }

    [TestMethod]
    public void Question_owner_and_revision_author_are_checked_separately()
    {
        var revisionAuthor = Guid.NewGuid();
        var draft = Snapshot(RevisionReviewState.Draft, 1) with
        {
            RevisionAuthorUserId = revisionAuthor
        };
        var ownerRole = RoleAssignment.Create(author, AppRole.Teacher, school, department);
        var authorRole = RoleAssignment.Create(revisionAuthor, AppRole.Teacher, school, department);

        Assert.IsTrue(ReviewWorkflow.Submit(draft, author, 1, [ownerRole]).Succeeded);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Submit(draft, revisionAuthor, 1, [authorRole]).Failure);

        var pending = draft with { State = RevisionReviewState.ReviewRequired, Version = 2 };
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, Decision(pending, revisionAuthor, ReviewDecision.Approve, 2),
                [RoleAssignment.Create(revisionAuthor, AppRole.DepartmentHead, school, department)]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Forbidden,
            ReviewWorkflow.Decide(pending, Decision(pending, author, ReviewDecision.Approve, 2),
                [RoleAssignment.Create(author, AppRole.DepartmentHead, school, department)]).Failure);
    }

    [TestMethod]
    public void Reject_requires_reason_and_decision_requires_current_revision_and_version()
    {
        var pending = Snapshot(RevisionReviewState.ReviewRequired, 2);
        var role = Head();

        Assert.AreEqual(ReviewTransitionFailure.InvalidRequest,
            ReviewWorkflow.Decide(pending,
                Decision(pending, reviewer, ReviewDecision.Reject, 2, " "), [role]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Conflict,
            ReviewWorkflow.Decide(pending,
                Decision(pending, reviewer, ReviewDecision.Approve, 1), [role]).Failure);
        Assert.AreEqual(ReviewTransitionFailure.Conflict,
            ReviewWorkflow.Decide(pending,
                Decision(pending with { Revision = QuestionRevisionRef.Create(Guid.NewGuid(), 1) },
                    reviewer, ReviewDecision.Approve, 2), [role]).Failure);
    }

    [TestMethod]
    public void Second_reviewer_cannot_decide_an_already_approved_revision()
    {
        var pending = Snapshot(RevisionReviewState.ReviewRequired, 2);
        var first = ReviewWorkflow.Decide(pending,
            Decision(pending, reviewer, ReviewDecision.Approve, 2), [Head()]);
        var secondReviewer = Guid.NewGuid();

        Assert.AreEqual(ReviewTransitionFailure.Conflict,
            ReviewWorkflow.Decide(first.Updated!,
                Decision(pending, secondReviewer, ReviewDecision.Reject, 2, "Too late"),
                [RoleAssignment.Create(secondReviewer, AppRole.DepartmentHead, school, department)]).Failure);
    }

    private ReviewRevisionSnapshot Snapshot(RevisionReviewState state, long version) =>
        new(QuestionRevisionRef.Create(Guid.NewGuid(), 1), school, department, author, author, state, version);

    private RoleAssignment Head() => RoleAssignment.Create(reviewer, AppRole.DepartmentHead, school, department);

    private static ReviewDecisionRequest Decision(ReviewRevisionSnapshot snapshot, Guid actor,
        ReviewDecision decision, long version, string reason = "") =>
        new(snapshot.Revision, actor, decision, reason, version);
}
