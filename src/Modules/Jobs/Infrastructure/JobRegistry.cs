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

    public JobRegistry()
    {
    }

    public JobRegistry(IEnumerable<IJobHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        foreach (var handler in handlers)
        {
            Register(handler);
        }
    }

    public void Register(IJobHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.JobType))
        {
            throw new ArgumentException("JobType cannot be null or whitespace.", nameof(handler));
        }

        _handlers[handler.JobType] = handler;
    }

    public IJobHandler? GetHandler(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType)) return null;
        return _handlers.TryGetValue(jobType, out var handler) ? handler : null;
    }

    public bool HasHandler(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType)) return false;
        return _handlers.ContainsKey(jobType);
    }

    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys.ToList();
}
