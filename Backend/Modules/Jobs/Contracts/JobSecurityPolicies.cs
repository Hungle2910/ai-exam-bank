using System.Security.Claims;

namespace AiExamBank.Modules.Jobs.Contracts;

/// <summary>
/// Các chính sách phân quyền và kiểm tra đặc quyền cho phân hệ Job Engine (M5-05).
/// Đảm bảo xác thực dựa trên Role/Claim/Scope được cấp phép rõ ràng (Explicit Claims),
/// tuyệt đối không suy diễn quyền từ tên tài khoản (UserId) nhằm ngăn chặn lỗ hổng CWE-285, CWE-863.
/// </summary>
public static class JobSecurityPolicies
{
    public const string MetricsPermission = "jobs.metrics";
    public const string AdminPermission = "jobs.admin";

    /// <summary>
    /// Kiểm tra người dùng có quyền Quản trị viên hệ thống hoặc thẩm quyền cấp Bộ hay không.
    /// </summary>
    public static bool IsSystemAdministrator(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // 1. Kiểm tra Role claim được cấp rõ ràng ("Admin", "SystemAdmin")
        if (principal.IsInRole("Admin") ||
            principal.IsInRole("SystemAdmin") ||
            principal.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type == "role") &&
                (string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Value, "SystemAdmin", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // 2. Kiểm tra ScopeKind được cấp rõ ràng là cấp Bộ (MinistryExam)
        var scopeKindClaim = principal.FindFirst("scope_kind")?.Value;
        if (string.Equals(scopeKindClaim, nameof(JobScopeKind.MinistryExam), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. Kiểm tra Permission claim chuyên biệt
        if (principal.HasClaim("permission", MetricsPermission) ||
            principal.HasClaim("permission", AdminPermission))
        {
            return true;
        }

        return false;
    }
}
