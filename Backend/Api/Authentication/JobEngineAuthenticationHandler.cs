using System.Security.Claims;
using System.Text.Encodings.Web;
using AiExamBank.Modules.Jobs.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiExamBank.Api.Authentication;

/// <summary>
/// Bản ghi danh tính người dùng được cấu hình an toàn cho môi trường chạy ứng dụng.
/// </summary>
/// <param name="UserId">Mã định danh người dùng.</param>
/// <param name="ScopeId">Mã phạm vi (trường / tổ chức / kỳ thi) được ủy quyền.</param>
/// <param name="ScopeKind">Loại phạm vi phân quyền (School hoặc MinistryExam).</param>
public sealed record TrustedDevIdentity(
    string UserId,
    string ScopeId,
    JobScopeKind ScopeKind = JobScopeKind.School);

/// <summary>
/// Trình xử lý xác thực danh tính cho dịch vụ Job Engine (JobEngine Authentication Handler).
/// Xác thực token dạng Bearer dựa trên cấu hình bí mật của máy chủ hoặc danh mục cách ly trong môi trường Development,
/// ánh xạ người dùng cùng phạm vi (scope claims: scope_id, scope_kind, school_id) vào HttpContext.User.
/// Chặn đứng tấn công vượt quyền và bảo đảm nguyên tắc Zero-Trust, từ chối mọi token do Client tự tạo (CWE-287, CWE-862, CWE-798).
/// </summary>
public sealed class JobEngineAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Tên lược đồ xác thực chuẩn cho Job Engine.
    /// </summary>
    public const string SchemeName = "JobEngineAuth";

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Danh sách token danh tính được cấp phép cục bộ cho môi trường Development / Smoke test nội bộ.
    /// Tuyệt đối bị vô hiệu hóa và từ chối nghiêm ngặt với HTTP 401 trên các môi trường triển khai (Production/Staging).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TrustedDevIdentity> DevelopmentTokens =
        new Dictionary<string, TrustedDevIdentity>(StringComparer.Ordinal)
        {
            ["dev-system-token-hn"] = new("system-user", "school-hn-01", JobScopeKind.School),
            ["dev-teacher-hn-token"] = new("teacher-hn", "school-hn-01", JobScopeKind.School),
            ["dev-teacher-sg-token"] = new("teacher-sg", "school-sg-02", JobScopeKind.School),
            ["smoke-test-token-hn"] = new("smoke-tester", "school-hn-01", JobScopeKind.School),
            ["smoke-test-token-sg"] = new("smoke-tester-sg", "school-sg-02", JobScopeKind.School),
            ["dev-ministry-token"] = new("ministry-officer", "national-exam-2026", JobScopeKind.MinistryExam)
        };

    [Obsolete("Use DevelopmentTokens or configure tokens via IConfiguration")]
    public static IReadOnlyDictionary<string, TrustedDevIdentity> TrustedTokens => DevelopmentTokens;

    public JobEngineAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment environment,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _environment = environment;
        _configuration = configuration;
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

        // 2. Xác minh token:
        // Trong môi trường Production/Staging: Bắt buộc đọc từ cấu hình bí mật (IConfiguration: JobEngine:TrustedTokens:<token>).
        // Token dev tĩnh tuyệt đối không được cấp quyền ngoài môi trường Development (CWE-798).
        TrustedDevIdentity? identityConfig = null;

        var secretSection = _configuration.GetSection($"JobEngine:TrustedTokens:{token}");
        if (secretSection.Exists())
        {
            var userId = secretSection["UserId"];
            var scopeId = secretSection["ScopeId"];
            var scopeKindStr = secretSection["ScopeKind"];
            var scopeKind = JobScopeKind.School;
            if (!string.IsNullOrWhiteSpace(scopeKindStr) &&
                Enum.TryParse<JobScopeKind>(scopeKindStr, ignoreCase: true, out var parsedKind) &&
                Enum.IsDefined(parsedKind))
            {
                scopeKind = parsedKind;
            }

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(scopeId))
            {
                identityConfig = new TrustedDevIdentity(userId, scopeId, scopeKind);
            }
        }

        if (identityConfig is null && _environment.IsDevelopment())
        {
            DevelopmentTokens.TryGetValue(token, out identityConfig);
        }

        if (identityConfig is null)
        {
            if (!_environment.IsDevelopment() && DevelopmentTokens.ContainsKey(token))
            {
                return Task.FromResult(AuthenticateResult.Fail(
                    "Development tokens are prohibited in non-development environments. Configure secrets via JobEngine:TrustedTokens."));
            }

            return Task.FromResult(AuthenticateResult.Fail("Untrusted or invalid Bearer token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityConfig.UserId),
            new(ClaimTypes.Name, identityConfig.UserId),
            new("scope_kind", identityConfig.ScopeKind.ToString()),
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
