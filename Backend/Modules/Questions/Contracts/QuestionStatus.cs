namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>Lifecycle of one immutable revision. Only the Review module moves a revision beyond <see cref="Draft"/>.</summary>
public enum QuestionStatus
{
    Draft = 1,
    ReviewRequired = 2,
    Approved = 3,
    Rejected = 4
}
