using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using AiExamBank.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Cấu hình kho lưu trữ Job bền vững và danh mục Handler
builder.Services.AddSingleton<IJobStore, FileJobStore>();
builder.Services.AddSingleton<JobRegistry>();

// Đăng ký các Handler thực thi tác vụ
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, ExamGenerationJobHandler>();
builder.Services.AddSingleton<IJobHandler, CsvImportJobHandler>();

// Đăng ký IJobRegistry phân giải từ JobRegistry cụ thể, tránh đệ quy
builder.Services.AddSingleton<IJobRegistry>(sp =>
{
    var registry = sp.GetRequiredService<JobRegistry>();
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
