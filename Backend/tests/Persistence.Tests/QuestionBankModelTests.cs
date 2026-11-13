using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AiExamBank.Persistence.Tests;

/// <summary>Checks the EF model and seed without a database; building a model never opens a connection.</summary>
[TestClass]
public sealed class QuestionBankModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AiExamBankDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new AiExamBankDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entity<T>(IModel model) =>
        model.FindEntityType(typeof(T)) ?? throw new AssertFailedException($"{typeof(T).Name} is not mapped.");

    [TestMethod]
    public void Seed_has_three_subjects_with_three_unique_topics_each()
    {
        Assert.HasCount(3, QuestionTaxonomySeed.Subjects);
        Assert.HasCount(9, QuestionTaxonomySeed.Topics);
        Assert.HasCount(12, QuestionTaxonomySeed.Subjects.Select(item => item.Id)
            .Concat(QuestionTaxonomySeed.Topics.Select(item => item.Id)).Distinct());
        Assert.HasCount(9, QuestionTaxonomySeed.Topics.Select(item => item.Code).Distinct());

        foreach (var subject in QuestionTaxonomySeed.Subjects)
        {
            var topics = QuestionTaxonomySeed.Topics.Where(item => item.SubjectId == subject.Id).ToArray();
            Assert.HasCount(3, topics, subject.Code);
            Assert.IsTrue(topics.All(item => item.Code.StartsWith(subject.Code + ".", StringComparison.Ordinal)), subject.Code);
        }
    }

    [TestMethod]
    public void Revision_is_keyed_by_question_and_revision_number()
    {
        var key = Entity<QuestionRevisionEntity>(BuildModel()).FindPrimaryKey()!;

        CollectionAssert.AreEqual(
            new[] { nameof(QuestionRevisionEntity.QuestionId), nameof(QuestionRevisionEntity.Revision) },
            key.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void A_retried_import_row_is_blocked_by_a_unique_filtered_index()
    {
        var index = Entity<QuestionRevisionEntity>(BuildModel()).GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(QuestionRevisionEntity.ImportBatchId), nameof(QuestionRevisionEntity.ImportRowNumber)]));

        Assert.IsTrue(index.IsUnique);
        Assert.AreEqual("[ImportBatchId] IS NOT NULL", index.GetFilter());
    }

    [TestMethod]
    public void The_question_bank_never_cascades_a_delete()
    {
        var model = BuildModel();
        var bankTypes = new[]
        {
            typeof(SubjectEntity), typeof(TopicEntity), typeof(SchoolSubjectEntity), typeof(QuestionEntity),
            typeof(QuestionRevisionEntity), typeof(QuestionOptionEntity)
        };

        foreach (var type in bankTypes)
        {
            foreach (var foreignKey in Entity(model, type).GetForeignKeys())
                Assert.AreEqual(DeleteBehavior.Restrict, foreignKey.DeleteBehavior, $"{type.Name} -> {foreignKey.PrincipalEntityType.ClrType.Name}");
        }
    }

    [TestMethod]
    public void Question_scope_must_match_the_department_that_owns_the_subject()
    {
        var question = Entity<QuestionEntity>(BuildModel());

        var foreignKey = question.GetForeignKeys().Single(item => item.PrincipalEntityType.ClrType == typeof(SchoolSubjectEntity));

        CollectionAssert.AreEquivalent(
            new[] { nameof(QuestionEntity.SchoolId), nameof(QuestionEntity.SubjectId), nameof(QuestionEntity.DepartmentId) },
            foreignKey.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void A_topic_must_belong_to_the_subject_of_the_revision()
    {
        var revision = Entity<QuestionRevisionEntity>(BuildModel());

        var foreignKey = revision.GetForeignKeys().Single(item => item.PrincipalEntityType.ClrType == typeof(TopicEntity));

        CollectionAssert.AreEquivalent(
            new[] { nameof(QuestionRevisionEntity.TopicId), nameof(QuestionRevisionEntity.SubjectId) },
            foreignKey.Properties.Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void Question_has_a_concurrency_token_and_revisions_store_enums_as_text()
    {
        var model = BuildModel();

        Assert.IsTrue(Entity<QuestionEntity>(model).FindProperty(nameof(QuestionEntity.RowVersion))!.IsConcurrencyToken);
        var status = Entity<QuestionRevisionEntity>(model).FindProperty(nameof(QuestionRevisionEntity.Status))!;
        Assert.AreEqual(typeof(string), status.GetProviderClrType());
    }

    private static IEntityType Entity(IModel model, Type type) =>
        model.FindEntityType(type) ?? throw new AssertFailedException($"{type.Name} is not mapped.");
}
