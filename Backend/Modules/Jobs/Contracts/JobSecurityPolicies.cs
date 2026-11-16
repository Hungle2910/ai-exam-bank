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
    /// Kiểm tra người dùng có quyền Quản trị viên hệ thống để truy cập metrics và cảnh báo sự cố hay không.
    /// Yêu cầu Role hoặc Permission được cấp rõ ràng (Explicit Role/Claim),
    /// tuyệt đối không suy diễn từ Scope (kể cả MinistryExam) hay tên người dùng (UserId) nhằm ngăn chặn CWE-285, CWE-863.
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

        // 2. Kiểm tra Permission claim chuyên biệt được cấp rõ ràng
        if (principal.HasClaim("permission", MetricsPermission) ||
            principal.HasClaim("permission", AdminPermission))
        {
            return true;
        }

        return false;
    }
}
