using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using AiExamBank.Persistence.Questions;

namespace AiExamBank.Persistence;

public sealed class AiExamBankDbContext : IdentityUserContext<AppUser, Guid>
{
    public AiExamBankDbContext(DbContextOptions<AiExamBankDbContext> options) //quản lý bảng người dùng Identity
        : base(options)
    {

    }
    public DbSet<RoleAssignmentEntity> RoleAssignments => Set<RoleAssignmentEntity>();//RoleAssignmentEntity vào mô hình EF Core
    public DbSet<SchoolEntity> Schools => Set<SchoolEntity>();
    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();
    public DbSet<RoleAssignmentAuditEntity> RoleAssignmentAudits => Set<RoleAssignmentAuditEntity>();
    public DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();
    public DbSet<TopicEntity> Topics => Set<TopicEntity>();
    public DbSet<SchoolSubjectEntity> SchoolSubjects => Set<SchoolSubjectEntity>();
    public DbSet<QuestionEntity> Questions => Set<QuestionEntity>();
    public DbSet<QuestionRevisionEntity> QuestionRevisions => Set<QuestionRevisionEntity>();
    public DbSet<QuestionOptionEntity> QuestionOptions => Set<QuestionOptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) //liên kết mỗi vai trò với một người dùng và lưu tên vai trò dạng chữ
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SchoolEntity>(entity =>
        {
            entity.ToTable("Schools");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<DepartmentEntity>(entity =>
        {
            entity.ToTable("Departments");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasAlternateKey(x => new { x.Id, x.SchoolId });
            entity.HasOne<SchoolEntity>().WithMany()
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>()
            .HasOne<SchoolEntity>().WithMany()
            .HasForeignKey(user => user.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoleAssignmentEntity>(entity =>
        {
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SchoolEntity>().WithMany()
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<DepartmentEntity>().WithMany()
                .HasForeignKey(x => new { x.DepartmentId, x.SchoolId })
                .HasPrincipalKey(x => new { x.Id, x.SchoolId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.UserId, x.Role, x.SchoolId, x.DepartmentId })
                .IsUnique()
                .HasFilter(null);

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.ToTable(table => table.HasCheckConstraint(
        "CK_RoleAssignments_Scope",
        """
        ([Role] = 'MinistryAdmin' AND [SchoolId] IS NULL AND [DepartmentId] IS NULL)
         OR ([Role] = 'SchoolAdmin' AND [SchoolId] IS NOT NULL AND [DepartmentId] IS NULL)
        OR ([Role] IN ('Teacher', 'DepartmentHead') AND [SchoolId] IS NOT NULL AND [DepartmentId] IS NOT NULL)
        """));
        });

        modelBuilder.Entity<RoleAssignmentAuditEntity>(entity =>
        {
            entity.ToTable("RoleAssignmentAudits");
            entity.Property(x => x.Action).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasOne<AppUser>().WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ApplyQuestionBank();
    }
}
