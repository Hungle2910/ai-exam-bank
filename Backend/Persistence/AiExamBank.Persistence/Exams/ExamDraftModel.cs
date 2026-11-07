using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

internal static class ExamDraftModel
{
    internal static void ApplyExamDrafts(this ModelBuilder model)
    {
        model.Entity<ExamDraftBatchEntity>(b =>
        {
            b.ToTable("ExamDraftBatches");
            b.HasKey(x => new { x.ExamId, x.JobId });
            b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            b.HasOne<ExamGenerationEntity>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExamDraftSlotEntity>(b =>
        {
            b.ToTable("ExamDraftSlots");
            b.HasKey(x => new { x.ExamId, x.JobId, x.SlotIndex });
            b.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasMaxLength(32).IsRequired();
            b.Property(x => x.ErrorCode).HasMaxLength(128);
            b.HasOne<ExamDraftBatchEntity>().WithMany().HasForeignKey(x => new { x.ExamId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExamDraftLinkEntity>(b =>
        {
            b.ToTable("ExamDraftLinks");
            b.HasKey(x => new { x.ExamId, x.SlotIndex, x.Ordinal });
            b.Property(x => x.SourceId).HasMaxLength(1000).IsRequired();
            b.Property(x => x.SourceLocator).HasMaxLength(1000).IsRequired();
            b.HasOne<ExamDraftSlotEntity>().WithMany().HasForeignKey(x => new { x.ExamId, x.JobId, x.SlotIndex }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
