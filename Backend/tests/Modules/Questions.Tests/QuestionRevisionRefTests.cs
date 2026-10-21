using AiExamBank.Modules.Questions.Contracts;

namespace AiExamBank.Modules.Questions.Tests;

[TestClass]
public sealed class QuestionRevisionRefTests
{
    [TestMethod]
    public void Create_preserves_the_exact_revision()
    {
        var questionId = Guid.NewGuid();

        var reference = QuestionRevisionRef.Create(questionId, 3);

        Assert.AreEqual(questionId, reference.QuestionId);
        Assert.AreEqual(3, reference.Revision);
    }

    [TestMethod]
    public void Create_rejects_a_missing_question_id()
    {
        Assert.ThrowsExactly<ArgumentException>(() => QuestionRevisionRef.Create(Guid.Empty, 1));
    }

    [TestMethod]
    public void Create_rejects_a_nonpositive_revision()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuestionRevisionRef.Create(Guid.NewGuid(), 0));
    }
}
