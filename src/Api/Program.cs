using AiExamBank.Api.Endpoints;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

// Cấu hình Job Engine Services cho API host
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<IJobRegistry, JobRegistry>();
builder.Services.AddSingleton<JobService>();
builder.Services.AddSingleton<IJobPublisher>(sp => sp.GetRequiredService<JobService>());
builder.Services.AddSingleton<IJobTracker>(sp => sp.GetRequiredService<JobService>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles();

// A liveness probe only: it must not imply database, worker or AWS readiness.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

// Đăng ký các endpoints nghiệp vụ cho Background Jobs
app.MapJobEndpoints();

app.Run();
