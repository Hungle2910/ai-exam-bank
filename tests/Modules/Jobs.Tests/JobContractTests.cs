using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Tests;

[TestClass]
public sealed class JobContractTests
{
    [TestMethod]
    public void Scope_requires_a_valid_kind_and_resource()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => JobScope.Create((JobScopeKind)99, "school-a"));
        Assert.ThrowsExactly<ArgumentException>(() => JobScope.Create(JobScopeKind.School, " "));
    }

    [TestMethod]
    public void Request_preserves_scope_and_correlation_id()
    {
        var scope = JobScope.Create(JobScopeKind.MinistryExam, "event-n");

        var request = JobRequest.Create("ExamGeneration", "payload-ref", "official-1", scope, "key-1", "trace-1");

        Assert.AreSame(scope, request.Scope);
        Assert.AreEqual("trace-1", request.CorrelationId);
        Assert.AreEqual("key-1", request.IdempotencyKey);
    }

    [TestMethod]
    public void Request_rejects_missing_idempotency_key()
    {
        var scope = JobScope.Create(JobScopeKind.School, "school-a");

        Assert.ThrowsExactly<ArgumentException>(() =>
            JobRequest.Create("QuestionImport", "payload-ref", "teacher-1", scope, " ", "trace-2"));
    }
}
