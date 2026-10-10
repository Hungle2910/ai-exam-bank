# Persistence (decision pending)

The team has not accepted a DB engine/provider, schema or migration strategy. Add the persistence project only with the first real stored workflow, its migration and an integration test against a fresh database. A single migration stream must cover cross-module transactions such as question approval plus audit.

Modules own their repository contracts; the persistence adapter may reference modules, while modules must not reference the adapter. See [solution design](../../docs/DOTNET_SOLUTION_DESIGN.md).
