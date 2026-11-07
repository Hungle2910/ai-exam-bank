using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

internal static class ExamGenerationModel
{
    internal static void ApplyExamGeneration(this ModelBuilder model)
    {
        model.Entity<ExamGenerationEntity>(b =>
        {
            b.ToTable("ExamGenerations");
            b.HasKey(x => x.Id);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.Property(x => x.BlueprintVersion).HasMaxLength(16).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasMaxLength(32).IsRequired();
            b.HasIndex(x => new { x.OwnerUserId, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.Status, x.JobId });
            b.HasOne<BlueprintEntity>().WithMany().HasForeignKey(x => x.BlueprintId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Gaps).WithOne().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExamItemEntity>(b =>
        {
            b.ToTable("ExamItems");
            b.HasKey(x => new { x.ExamId, x.Position });
            b.HasIndex(x => new { x.ExamId, x.QuestionId }).IsUnique();
            b.Property(x => x.Points).HasPrecision(18, 4);
        });
        model.Entity<ExamGapEntity>(b =>
        {
            b.ToTable("ExamGapReports");
            b.HasKey(x => new { x.ExamId, x.SlotIndex });
            b.Property(x => x.Difficulty).HasMaxLength(8).IsRequired();
            b.Property(x => x.MissingPoints).HasPrecision(18, 4);
        });
    }
}
