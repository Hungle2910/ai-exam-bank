using System.Security.Cryptography;
using System.Text;
using AiExamBank.Modules.Jobs.Contracts;

namespace AiExamBank.Modules.Jobs.Domain;

/// <summary>
/// Các hàm tiện ích dùng chung để sinh khóa lũy quyền (Idempotency Key) và mã băm yêu cầu (Request Fingerprint)
/// theo chuẩn hóa không phân biệt hoa thường của phạm vi trường học / Bộ Giáo Dục.
/// </summary>
public static class JobIdempotency
{
    /// <summary>
    /// Xây dựng khóa idempotency duy nhất theo phạm vi, loại job và người yêu cầu.
    /// </summary>
    public static string BuildIdempotencyKey(JobScopeKind scopeKind, string scopeResourceId, string requesterId, string jobType, string idempotencyKey) =>
        $"{scopeKind}:{scopeResourceId.ToUpperInvariant()}:{requesterId}:{jobType}:{idempotencyKey}";

    /// <summary>
    /// Xây dựng khóa idempotency từ đối tượng JobRequest.
    /// </summary>
    public static string BuildIdempotencyKey(JobRequest request) =>
        BuildIdempotencyKey(request.Scope.Kind, request.Scope.ResourceId, request.RequesterId, request.JobType, request.IdempotencyKey);

    /// <summary>
    /// Tính toán mã băm SHA256 nhận diện tính bất biến của nội dung yêu cầu (Fingerprint).
    /// </summary>
    public static string ComputeRequestFingerprint(string jobType, string payloadReference, JobScopeKind scopeKind, string scopeResourceId)
    {
        var normalizedResourceId = scopeResourceId.ToUpperInvariant();
        var rawInput = $"{jobType}:{payloadReference}:{scopeKind}:{normalizedResourceId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawInput));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
