namespace AiExamBank.Api.InfrastructureHealth;

public sealed record ComponentHealth(string Name, string Status, DateTimeOffset? CheckedAt);

public sealed record HealthSnapshot(
    string Status,
    DateTimeOffset? CheckedAt,
    string Environment,
    object? Deployment,
    IReadOnlyList<ComponentHealth> Components,
    string TraceId)
{
    public static HealthSnapshot Unknown(string traceId) => new(
        "UNKNOWN", null, "staging", null,
        [new("database", "UNKNOWN", null), new("storage", "UNKNOWN", null), new("ai", "UNKNOWN", null)],
        traceId);
}
