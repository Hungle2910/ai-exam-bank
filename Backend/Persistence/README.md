# Persistence

The M3 branch implements the Identity slice with EF Core and SQL Server. `AiExamBankDbContext` contains users, school/department scope, role assignments and role-change audit. Its `InitialIdentity` and `SchoolScopeAndRoleAudit` migrations are the start of one migration stream. The final cross-module schema and hosting decision have not been recorded as a team-wide accepted ADR; this implementation does not claim that approval.

Apply migrations before starting the API or running the one-time MinistryAdmin bootstrap command. Do not put production connection strings or passwords in this repository. See [M3-02 feature notes](../docs/M3-02_feature_notes.md) for the current API and bootstrap instructions.

Modules own their repository contracts; the persistence adapter may reference modules, while modules must not reference the adapter. See [solution design](../docs/DOTNET_SOLUTION_DESIGN.md).
