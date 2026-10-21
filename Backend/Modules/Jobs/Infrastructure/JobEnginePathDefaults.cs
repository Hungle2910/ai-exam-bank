namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Quản lý đường dẫn cấu hình mặc định dùng chung giữa Web API và Worker độc lập.
/// </summary>
public static class JobEnginePathDefaults
{
    /// <summary>
    /// Phân giải đường dẫn tệp snapshot kho lưu trữ Job. Nếu được cấu hình thì bắt buộc phải là đường dẫn tuyệt đối;
    /// nếu không cấu hình thì sử dụng đường dẫn tuyệt đối mặc định dùng chung giữa API và Worker.
    /// </summary>
    public static string ResolveStorePath(string? configuredStorePath)
    {
        if (!string.IsNullOrWhiteSpace(configuredStorePath))
        {
            if (!Path.IsPathFullyQualified(configuredStorePath))
            {
                throw new InvalidOperationException("Jobs:StorePath must be configured as a fully qualified absolute path.");
            }
            return configuredStorePath;
        }

        // Đường dẫn tuyệt đối mặc định dùng chung cho cả Web API và Worker độc lập
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ai-exam-bank", "jobs-store.json"));
    }
}
