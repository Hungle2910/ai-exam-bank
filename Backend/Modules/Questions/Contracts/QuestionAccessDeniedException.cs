namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>The actor's current assignments do not allow the action. The API maps this to 403.</summary>
public sealed class QuestionAccessDeniedException(string message) : Exception(message);
