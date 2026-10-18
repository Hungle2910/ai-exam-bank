using AiExamBank.Api.Authentication;
using AiExamBank.Api.Endpoints;
using AiExamBank.Api.HostedServices;
using AiExamBank.Api.InfrastructureHealth;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

// Register the M2 infrastructure health policy before the host authentication
// scheme so the shared API authentication remains the active default scheme.
builder.Services.AddInfrastructureHealth();

// API host authentication for the job engine. The handler is fail-closed until
// a caller presents the configured identity headers.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JobEngineAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = JobEngineAuthenticationHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, JobEngineAuthenticationHandler>(
    JobEngineAuthenticationHandler.SchemeName,
    _ => { });

builder.Services.AddAuthorization();

var configuredStorePath = JobEnginePathDefaults.ResolveStorePath(builder.Configuration["Jobs:StorePath"]);

builder.Services.AddSingleton<IJobStore>(_ => new FileJobStore(configuredStorePath));
builder.Services.AddSingleton<JobRegistry>();

// Register the supported job handlers.
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, ExamGenerationJobHandler>();
builder.Services.AddSingleton<IJobHandler, CsvImportJobHandler>();

// Resolve the safe registry from the registered handlers.
builder.Services.AddSingleton<IJobRegistry>(sp =>
{
    var registry = sp.GetRequiredService<JobRegistry>();
    foreach (var handler in sp.GetServices<IJobHandler>())
    {
        registry.Register(handler);
    }

    return registry;
});

builder.Services.AddSingleton<JobService>();
builder.Services.AddSingleton<IJobPublisher>(sp => sp.GetRequiredService<JobService>());
builder.Services.AddSingleton<IJobTracker>(sp => sp.GetRequiredService<JobService>());

// The embedded worker can be disabled when a separate Worker process is used.
if (!builder.Configuration.GetValue<bool>("Jobs:DisableEmbeddedWorker"))
{
    builder.Services.AddHostedService<JobWorkerHostedService>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

// Serve the job status UI and the infrastructure health panel assets.
app.UseDefaultFiles();
app.UseStaticFiles();

// A liveness probe only: it must not imply database, worker or AWS readiness.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapInfrastructureHealth();

// Background job endpoints.
app.MapJobEndpoints();

app.Run();
