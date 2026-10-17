using AiExamBank.Api.Authentication;
using AiExamBank.Api.Endpoints;
using AiExamBank.Api.HostedServices;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

// Cấu hình xác thực danh tính cho API host qua JobEngineAuthenticationHandler
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JobEngineAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = JobEngineAuthenticationHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, JobEngineAuthenticationHandler>(
    JobEngineAuthenticationHandler.SchemeName,
    _ => { });

builder.Services.AddAuthorization();

// Cấu hình Job Engine Services cho API host:
// Sử dụng FileJobStore để lưu trữ bền vững qua các lần khởi động lại và chia sẻ cùng tiến trình Worker
builder.Services.AddSingleton<IJobStore, FileJobStore>();
builder.Services.AddSingleton<JobRegistry>();

// Đăng ký các Handler thực thi tác vụ
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, ExamGenerationJobHandler>();
builder.Services.AddSingleton<IJobHandler, CsvImportJobHandler>();

// Đăng ký IJobRegistry phân giải an toàn từ JobRegistry
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

// Đăng ký dịch vụ nền thực thi Worker ngay trong API process
builder.Services.AddHostedService<JobWorkerHostedService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

// Chỉ phục vụ giao diện tĩnh và công cụ kiểm thử Job UI trong môi trường Development (CWE-798)
if (app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// A liveness probe only: it must not imply database, worker or AWS readiness.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

// Đăng ký các endpoints nghiệp vụ cho Background Jobs
app.MapJobEndpoints();

app.Run();
