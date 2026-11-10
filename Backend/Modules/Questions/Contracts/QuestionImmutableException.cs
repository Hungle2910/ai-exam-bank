namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>
/// Something tried to change or delete a revision's content, an option, or a revision that was already decided.
/// Content is written once; a change is a new revision. This is a programming error, not a user error, so the API
/// does not map it to a client status.
/// </summary>
public sealed class QuestionImmutableException(string message) : InvalidOperationException(message);
