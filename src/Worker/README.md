# Worker host (planned)

The solution does not start a background processor yet. M5 will add this project with the first durable job handler, lease/retry implementation and restart tests. A process that starts without processing jobs would give a false readiness signal.

The planned Worker will reference the Jobs module and the chosen persistence adapter. It will use a separate deployment process from the API, cancellation-aware hosted services, and an explicit readiness check for the dependencies it actually needs. See [solution design](../../docs/DOTNET_SOLUTION_DESIGN.md).
