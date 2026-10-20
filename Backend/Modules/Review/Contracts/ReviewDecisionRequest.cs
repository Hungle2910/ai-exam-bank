using AiExamBank.Modules.Questions.Contracts;

namespace AiExamBank.Modules.Review.Contracts;

public enum ReviewDecision { Approve, Reject }

/// <summary>Input to a future atomic review + revision-state transition.</summary>
public sealed record ReviewDecisionRequest(
    QuestionRevisionRef Revision,
    Guid ReviewerId,
    ReviewDecision Decision,
    string Reason,
    long ExpectedVersion);
