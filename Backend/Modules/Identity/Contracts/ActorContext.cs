namespace AiExamBank.Modules.Identity.Contracts;

/// <summary>The authenticated actor supplied by the API boundary to a use case.</summary>
public sealed record ActorContext
{
    private ActorContext(Guid userId, string role)
    {
        UserId = userId;
        Role = role;
    }

    public Guid UserId { get; }
    public string Role { get; }

    public static ActorContext Create(Guid userId, string role)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("A role is required.", nameof(role));
        return new ActorContext(userId, role.Trim());
    }
}
