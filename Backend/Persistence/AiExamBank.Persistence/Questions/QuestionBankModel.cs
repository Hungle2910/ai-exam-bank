using AiExamBank.Modules.Questions.Contracts;
using AiExamBank.Modules.Questions.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Questions;

/// <summary>EF mapping for the question bank. Nothing here cascades: questions and revisions are never deleted.</summary>
internal static class QuestionBankModel
{
    public static void ApplyQuestionBank(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubjectEntity>(entity =>
        {
            entity.ToTable("Subjects");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasData(QuestionTaxonomySeed.Subjects);
        });

        modelBuilder.Entity<TopicEntity>(entity =>
        {
            entity.ToTable("Topics");
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasAlternateKey(x => new { x.Id, x.SubjectId });
            entity.HasIndex(x => new { x.SubjectId, x.Code }).IsUnique();
            entity.HasOne<SubjectEntity>().WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasData(QuestionTaxonomySeed.Topics);
        });

        modelBuilder.Entity<SchoolSubjectEntity>(entity =>
        {
            entity.ToTable("SchoolSubjects");
            entity.HasKey(x => new { x.SchoolId, x.SubjectId });
            entity.HasAlternateKey(x => new { x.SchoolId, x.SubjectId, x.DepartmentId });
            entity.HasIndex(x => x.DepartmentId);
            entity.HasOne<SchoolEntity>().WithMany()
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SubjectEntity>().WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DepartmentEntity>().WithMany()
                .HasForeignKey(x => new { x.DepartmentId, x.SchoolId })
                .HasPrincipalKey(x => new { x.Id, x.SchoolId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuestionEntity>(entity =>
        {
            entity.ToTable("Questions", table => table.HasCheckConstraint(
                "CK_Questions_LatestRevision", "[LatestRevision] >= 1"));
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasAlternateKey(x => new { x.Id, x.SubjectId });
            entity.HasIndex(x => new { x.SchoolId, x.SubjectId, x.UpdatedAtUtc });
            entity.HasIndex(x => x.OwnerUserId);
            // The department must be the one that owns this subject in this school, so scope cannot drift from the catalog.
            entity.HasOne<SchoolSubjectEntity>().WithMany()
                .HasForeignKey(x => new { x.SchoolId, x.SubjectId, x.DepartmentId })
                .HasPrincipalKey(x => new { x.SchoolId, x.SubjectId, x.DepartmentId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuestionRevisionEntity>(entity =>
        {
            entity.ToTable("QuestionRevisions", table =>
            {
                table.HasCheckConstraint("CK_QuestionRevisions_Revision", "[Revision] >= 1");
                table.HasCheckConstraint("CK_QuestionRevisions_CorrectOption", "[CorrectOption] IN ('A', 'B', 'C', 'D')");
                table.HasCheckConstraint("CK_QuestionRevisions_Status", InList("Status", Enum.GetNames<QuestionStatus>()));
                table.HasCheckConstraint("CK_QuestionRevisions_Origin", InList("Origin", Enum.GetNames<QuestionOrigin>()));
                table.HasCheckConstraint("CK_QuestionRevisions_Difficulty", InList("Difficulty", Enum.GetNames<Difficulty>()));
                table.HasCheckConstraint(
                    "CK_QuestionRevisions_ImportRow",
                    $"([ImportBatchId] IS NULL AND [ImportRowNumber] IS NULL AND [Origin] <> '{nameof(QuestionOrigin.Import)}') " +
                    $"OR ([ImportBatchId] IS NOT NULL AND [ImportRowNumber] >= 1 AND [Origin] = '{nameof(QuestionOrigin.Import)}')");
            });
            entity.HasKey(x => new { x.QuestionId, x.Revision });
            entity.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Origin).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Stem).HasMaxLength(QuestionRules.MaxStemLength).IsRequired();
            entity.Property(x => x.CorrectOption).HasMaxLength(1).IsRequired();
            entity.Property(x => x.SourceText).HasMaxLength(QuestionRules.MaxSourceLength);
            // Reading the bank by topic, difficulty and status is the hot path for search and exam selection.
            entity.HasIndex(x => new { x.TopicId, x.Difficulty, x.Status });
            // A retried import commit cannot write the same CSV row twice.
            entity.HasIndex(x => new { x.ImportBatchId, x.ImportRowNumber })
                .IsUnique()
                .HasFilter("[ImportBatchId] IS NOT NULL");
            entity.HasOne<QuestionEntity>().WithMany()
                .HasForeignKey(x => new { x.QuestionId, x.SubjectId })
                .HasPrincipalKey(x => new { x.Id, x.SubjectId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TopicEntity>().WithMany()
                .HasForeignKey(x => new { x.TopicId, x.SubjectId })
                .HasPrincipalKey(x => new { x.Id, x.SubjectId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuestionOptionEntity>(entity =>
        {
            entity.ToTable("QuestionOptions", table => table.HasCheckConstraint(
                "CK_QuestionOptions_Label", "[Label] IN ('A', 'B', 'C', 'D')"));
            entity.HasKey(x => new { x.QuestionId, x.Revision, x.Label });
            entity.Property(x => x.Label).HasMaxLength(1).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(QuestionRules.MaxOptionLength).IsRequired();
            entity.HasOne<QuestionRevisionEntity>().WithMany()
                .HasForeignKey(x => new { x.QuestionId, x.Revision })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static string InList(string column, IEnumerable<string> names) =>
        $"[{column}] IN ({string.Join(", ", names.Select(name => $"'{name}'"))})";
}
