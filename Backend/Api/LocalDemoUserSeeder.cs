using AiExamBank.Persistence;
using AiExamBank.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiExamBank.Api;

internal static class LocalDemoUserSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() ||
            !app.Configuration.GetValue<bool>("LocalDemoUser:Enabled"))
        {
            return;
        }

        var password = app.Configuration["LocalDemoUser:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("LocalDemoUser:Password is required when local demo seeding is enabled.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AiExamBankDbContext>();
        const string userName = "demo.user";

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new AppUser { UserName = userName };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create local demo user: " + string.Join(", ", result.Errors.Select(error => error.Code)));
            }
        }

        if (!app.Configuration.GetValue<bool>("LocalDemoUser:BootstrapMinistryAdmin"))
            return;

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            throw new InvalidOperationException("Local demo password does not match the existing account.");
        }

        if (await db.RoleAssignments.AnyAsync(assignment =>
            assignment.UserId == user.Id && assignment.Role == AppRole.MinistryAdmin))
            return;

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
    }
}
