using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiExamBank.Api.Authentication;

/// <summary>
/// Bản ghi danh tính người dùng được cấu hình an toàn cho môi trường chạy ứng dụng.
/// </summary>
/// <param name="UserId">Mã định danh người dùng.</param>
/// <param name="ScopeId">Mã phạm vi (trường / tổ chức) được ủy quyền.</param>
public sealed record TrustedDevIdentity(string UserId, string ScopeId);

/// <summary>
/// Trình xử lý xác thực danh tính cho dịch vụ Job Engine (JobEngine Authentication Handler).
/// Xác thực token dạng Bearer dựa trên danh sách token được cấp phép của máy chủ,
/// ánh xạ người dùng cùng phạm vi (scope claims: scope_id, school_id) vào HttpContext.User.
/// Chặn đứng tấn công vượt quyền và bảo đảm nguyên tắc Zero-Trust, từ chối mọi token do Client tự tạo (CWE-287, CWE-862).
/// </summary>
public sealed class JobEngineAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Tên lược đồ xác thực chuẩn cho Job Engine.
    /// </summary>
    public const string SchemeName = "JobEngineAuth";

    /// <summary>
    /// Danh sách token danh tính được cấp phép và xác thực trước bởi hệ thống.
    /// Chỉ các token trong danh sách này mới được cấp quyền truy cập; từ chối mọi token do client tự tạo.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TrustedDevIdentity> TrustedTokens =
        new Dictionary<string, TrustedDevIdentity>(StringComparer.Ordinal)
        {
            ["dev-system-token-hn"] = new("system-user", "school-hn-01"),
            ["dev-teacher-hn-token"] = new("teacher-hn", "school-hn-01"),
            ["dev-teacher-sg-token"] = new("teacher-sg", "school-sg-02"),
            ["smoke-test-token-hn"] = new("smoke-tester", "school-hn-01"),
            ["smoke-test-token-sg"] = new("smoke-tester-sg", "school-sg-02")
        };

    public JobEngineAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Kiểm tra header Authorization (Bearer <token>)
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues) ||
            string.IsNullOrWhiteSpace(authHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = authHeaderValues.ToString().Trim();
        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format. Expected 'Bearer <token>'."));
        }

        var token = headerValue["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is empty."));
        }

        // 2. Xác minh token dựa trên danh mục token được cấp phép của máy chủ:
        // Tuyệt đối không chấp nhận token do client tự chế (ví dụ: user:scope) để triệt tiêu lỗ hổng CWE-287 và CWE-862.
        if (!TrustedTokens.TryGetValue(token, out var identityConfig))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Untrusted or invalid Bearer token '{token}'."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityConfig.UserId),
            new(ClaimTypes.Name, identityConfig.UserId),
            new("scope_id", identityConfig.ScopeId),
            new("school_id", identityConfig.ScopeId),
            new("scope", identityConfig.ScopeId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
