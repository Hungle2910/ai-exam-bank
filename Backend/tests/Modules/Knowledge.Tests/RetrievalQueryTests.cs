using AiExamBank.Modules.Knowledge.Contracts;

namespace AiExamBank.Modules.Knowledge.Tests;

[TestClass]
public sealed class RetrievalQueryTests
{
    [TestMethod]
    public void Create_keeps_the_scope_and_trims_the_query_text()
    {
        var actor = Guid.NewGuid();
        var school = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var topic = Guid.NewGuid();

        var query = RetrievalQuery.Create(actor, school, subject, topic, "  tích phân từng phần  ", 5);

        Assert.AreEqual(actor, query.ActorUserId);
        Assert.AreEqual(school, query.SchoolId);
        Assert.AreEqual(subject, query.SubjectId);
        Assert.AreEqual(topic, query.TopicId);
        Assert.AreEqual("tích phân từng phần", query.QueryText);
        Assert.AreEqual(5, query.MaxPassages);
    }

    [TestMethod]
    public void Create_requires_the_actor_and_every_scope_identifier()
    {
        var id = Guid.NewGuid();

        Assert.ThrowsExactly<ArgumentException>(() => RetrievalQuery.Create(Guid.Empty, id, id, id, "q", 1));
        Assert.ThrowsExactly<ArgumentException>(() => RetrievalQuery.Create(id, Guid.Empty, id, id, "q", 1));
        Assert.ThrowsExactly<ArgumentException>(() => RetrievalQuery.Create(id, id, Guid.Empty, id, "q", 1));
        Assert.ThrowsExactly<ArgumentException>(() => RetrievalQuery.Create(id, id, id, Guid.Empty, "q", 1));
    }

    [TestMethod]
    public void Create_rejects_blank_text_and_an_out_of_range_passage_count()
    {
        var id = Guid.NewGuid();

        Assert.ThrowsExactly<ArgumentException>(() => RetrievalQuery.Create(id, id, id, id, "  ", 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RetrievalQuery.Create(id, id, id, id, "q", 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => RetrievalQuery.Create(id, id, id, id, "q", RetrievalQuery.MaxPassagesLimit + 1));
    }
}
