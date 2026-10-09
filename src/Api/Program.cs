var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// A liveness probe only: it must not imply database, worker or AWS readiness.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

app.Run();
