namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>The revision a write produced. A newly written revision is always <see cref="QuestionStatus.Draft"/>.</summary>
public sealed record QuestionWriteResult(QuestionRevisionRef Revision, QuestionStatus Status);
