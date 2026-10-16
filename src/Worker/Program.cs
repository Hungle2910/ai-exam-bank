using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using AiExamBank.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Cấu hình kho lưu trữ Job và danh mục Handler
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<IJobRegistry, JobRegistry>();

// Đăng ký các Handler thực thi tác vụ
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, ExamGenerationJobHandler>();
builder.Services.AddSingleton<IJobHandler, CsvImportJobHandler>();

// Tự động đăng ký các IJobHandler vào IJobRegistry
builder.Services.AddSingleton(sp =>
{
    var registry = sp.GetRequiredService<IJobRegistry>();
    foreach (var handler in sp.GetServices<IJobHandler>())
    {
        registry.Register(handler);
    }
    return registry;
});

// Cấu hình và đăng ký Worker Background Service
builder.Services.Configure<JobWorkerOptions>(builder.Configuration.GetSection("JobWorker"));
builder.Services.AddHostedService<JobWorkerService>();

var host = builder.Build();

// Kích hoạt nạp Handler vào Registry
_ = host.Services.GetRequiredService<IJobRegistry>();

await host.RunAsync();
