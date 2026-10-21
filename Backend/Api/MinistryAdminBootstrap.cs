using System.Data;
using AiExamBank.Modules.Identity.Contracts;
using AiExamBank.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class MinistryAdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var userName = Environment.GetEnvironmentVariable("AIEXAMBANK_BOOTSTRAP_USERNAME")?.Trim();
        var password = Environment.GetEnvironmentVariable("AIEXAMBANK_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Set AIEXAMBANK_BOOTSTRAP_USERNAME and AIEXAMBANK_BOOTSTRAP_PASSWORD for this one-time command.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AiExamBankDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.RoleAssignments.AnyAsync(assignment => assignment.Role == AppRole.MinistryAdmin))
            throw new InvalidOperationException("A MinistryAdmin already exists. Bootstrap is one-time only.");

        var user = new AppUser { Id = Guid.NewGuid(), UserName = userName };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Could not create MinistryAdmin: " + string.Join(", ", created.Errors.Select(error => error.Code)));

        db.RoleAssignments.Add(new RoleAssignmentEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = AppRole.MinistryAdmin
        });
        db.RoleAssignmentAudits.Add(new RoleAssignmentAuditEntity
        {
            Id = Guid.NewGuid(),
            ActorUserId = user.Id,
            TargetUserId = user.Id,
            Action = "Bootstrapped",
            Role = AppRole.MinistryAdmin,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("MinistryAdmin bootstrap completed.");
    }
}
