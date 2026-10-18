using System.Collections.Concurrent;
using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Infrastructure;

/// <summary>
/// Bộ quản lý danh mục Handler an toàn đa luồng (Thread-safe Job Handler Registry).
/// Dùng để phân phối tác vụ cho Worker khi thực thi nền.
/// </summary>
public sealed class JobRegistry : IJobRegistry
{
    private readonly ConcurrentDictionary<string, IJobHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Khởi tạo Registry rỗng.
    /// </summary>
    public JobRegistry()
    {
    }

    /// <summary>
    /// Khởi tạo Registry và tự động nạp danh sách Handler ban đầu.
    /// </summary>
    /// <param name="handlers">Tập hợp các IJobHandler cần đăng ký.</param>
    public JobRegistry(IEnumerable<IJobHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        foreach (var handler in handlers)
        {
            Register(handler);
        }
    }

    /// <inheritdoc />
    public void Register(IJobHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.JobType))
        {
            throw new ArgumentException("JobType cannot be null or whitespace.", nameof(handler));
        }

        _handlers[handler.JobType] = handler;
    }

    /// <inheritdoc />
    public IJobHandler? GetHandler(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType)) return null;
        return _handlers.TryGetValue(jobType, out var handler) ? handler : null;
    }

    /// <inheritdoc />
    public bool HasHandler(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType)) return false;
        return _handlers.ContainsKey(jobType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys.ToList();
}
