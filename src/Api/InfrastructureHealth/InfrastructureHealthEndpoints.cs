using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiExamBank.Api.InfrastructureHealth;

public static class InfrastructureHealthEndpoints
{
    public const string ReadPolicy = "InfrastructureHealth.Read";
    private const string PendingScheme = "M2PendingAuthentication";

    public static IServiceCollection AddInfrastructureHealth(this IServiceCollection services)
    {
        // Fail closed until the shared authentication implementation replaces
        // this default scheme. No header, query parameter or local login grants access.
        services.AddAuthentication(PendingScheme)
            .AddScheme<AuthenticationSchemeOptions, PendingAuthenticationHandler>(PendingScheme, _ => { });
        services.AddAuthorization(options => options.AddPolicy(ReadPolicy, policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", ReadPolicy)));
        return services;
    }

    public static void MapInfrastructureHealth(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/infrastructure/health", Snapshot)
            .RequireAuthorization(ReadPolicy);
        app.MapGet("/admin/infrastructure", (HttpContext context) => Page(app, context))
            .RequireAuthorization(ReadPolicy);

        // Explicit opt-in preview: only Development AND loopback clients.
        // It never authenticates a user and never enables the protected routes.
        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("M2:EnableLocalPreview"))
        {
            app.MapGet("/dev/infrastructure-health", (HttpContext context) =>
                IsLoopback(context) ? Page(app, context) : Results.NotFound());
            app.MapGet("/dev/infrastructure-health/snapshot", (HttpContext context) =>
                IsLoopback(context) ? Snapshot(context) : Results.NotFound());
        }
    }

    private static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);

    private static IResult Page(WebApplication app, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'";
        var path = Path.Combine(AppContext.BaseDirectory, "InfrastructureHealth", "Panel", "index.html");
        return Results.File(path, "text/html; charset=utf-8");
    }

    private static IResult Snapshot(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(HealthSnapshot.Unknown(context.TraceIdentifier));
    }

    private sealed class PendingAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
