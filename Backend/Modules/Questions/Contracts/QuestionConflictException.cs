namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>The question changed since the caller read it. The API maps this to 409.</summary>
public sealed class QuestionConflictException(string message) : Exception(message);
