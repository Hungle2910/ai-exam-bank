using AiExamBank.Persistence.Questions;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Persistence.Exams;

internal static class BlueprintModel
{
    internal static void ApplyBlueprints(this ModelBuilder model)
    {
        model.Entity<BlueprintEntity>(b =>
        {
            b.ToTable("Blueprints");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.TotalPoints).HasPrecision(18, 4);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.OwnerUserId, x.SchoolId, x.UpdatedAtUtc });
            b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchoolSubjectEntity>().WithMany()
                .HasForeignKey(x => new { x.SchoolId, x.SubjectId, x.DepartmentId })
                .HasPrincipalKey(x => new { x.SchoolId, x.SubjectId, x.DepartmentId }).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Slots).WithOne().HasForeignKey(x => x.BlueprintId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<BlueprintSlotEntity>(b =>
        {
            b.ToTable("BlueprintSlots");
            b.HasKey(x => x.Id);
            b.Property(x => x.Difficulty).HasMaxLength(8).IsRequired();
            b.Property(x => x.PointsPerQuestion).HasPrecision(18, 4);
            b.HasIndex(x => new { x.BlueprintId, x.TopicId, x.Difficulty }).IsUnique();
            b.HasOne<TopicEntity>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
