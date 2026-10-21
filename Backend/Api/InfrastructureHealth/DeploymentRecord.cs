namespace AiExamBank.Api.InfrastructureHealth;

// Persistence-neutral entity preparation. No DbContext or database is created.
// Integrate with the shared SQL Server migration stream before storing records.
public sealed class DeploymentRecord
{
    public Guid Id { get; set; }
    public required string Environment { get; set; }
    public required string DeploymentId { get; set; }
    public required string SourceVersion { get; set; }
    public required string ArtifactVersion { get; set; }
    public required string DeployedBy { get; set; }
    public DateTime DeployedAt { get; set; } // UTC; SQL datetime2(3).
    public required string DeploymentStatus { get; set; }
    public string? Notes { get; set; }
}
