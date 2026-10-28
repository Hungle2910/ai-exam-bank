namespace AiExamBank.Modules.Questions.Contracts;

/// <summary>The question does not exist, or is outside what the actor may know about. The API maps this to 404.</summary>
public sealed class QuestionNotFoundException(string message) : Exception(message);
