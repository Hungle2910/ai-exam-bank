using AiExamBank.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AiExamBank.Api;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using AiExamBank.Api.Authentication;
using AiExamBank.Api.Endpoints;
using AiExamBank.Api.HostedServices;
using AiExamBank.Api.InfrastructureHealth;
using AiExamBank.Modules.Jobs.Contracts;
using AiExamBank.Modules.Jobs.Domain;
using AiExamBank.Modules.Jobs.Infrastructure;
using Microsoft.AspNetCore.Authentication;

var bootstrapMinistryAdmin = args.Length == 1 && args[0] == "--bootstrap-ministry-admin";
if (bootstrapMinistryAdmin &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings__DefaultConnection explicitly for the MinistryAdmin bootstrap command.");
}

var builder = WebApplication.CreateBuilder(bootstrapMinistryAdmin ? [] : args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing DefaultConnection.");
var sessionLifetimeSeconds = builder.Configuration.GetValue<int?>("Auth:SessionLifetimeSeconds") ?? 1800;
if (sessionLifetimeSeconds is < 1 or > 86400)
    throw new InvalidOperationException("Auth:SessionLifetimeSeconds must be between 1 and 86400.");

builder.Services.AddDbContext<AiExamBankDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<ResourceAuthorizationService>();
builder.Services.AddInfrastructureHealth();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme) //cấu hình cookie để lưu phiên đăng nhập
    .AddIdentityCookies();
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, JobEngineAuthenticationHandler>(
        JobEngineAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddIdentityCore<AppUser>() //đăng ký quản lý người dùng Identity với database
    .AddSignInManager() //sẽ dùng để xử lý đăng nhập và đăng xuất
    .AddEntityFrameworkStores<AiExamBankDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromSeconds(sessionLifetimeSeconds);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();//chặn API khi người dùng không có quyền
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddProblemDetails();
var configuredStorePath = JobEnginePathDefaults.ResolveStorePath(builder.Configuration["Jobs:StorePath"]);
builder.Services.AddSingleton<IJobStore>(_ => new FileJobStore(configuredStorePath));
builder.Services.AddSingleton<JobRegistry>();
builder.Services.AddSingleton<IAiProviderCircuitBreaker, InMemoryCircuitBreaker>();
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, ExamGenerationJobHandler>();
builder.Services.AddSingleton<IJobHandler, CsvImportJobHandler>();
builder.Services.AddSingleton<IJobRegistry>(sp =>
{
    var registry = sp.GetRequiredService<JobRegistry>();
    foreach (var handler in sp.GetServices<IJobHandler>())
        registry.Register(handler);
    return registry;
});
builder.Services.AddSingleton<JobService>();
builder.Services.AddSingleton<IJobPublisher>(sp => sp.GetRequiredService<JobService>());
builder.Services.AddSingleton<IJobTracker>(sp => sp.GetRequiredService<JobService>());
if (!builder.Configuration.GetValue<bool>("Jobs:DisableEmbeddedWorker"))
    builder.Services.AddHostedService<JobWorkerHostedService>();

var app = builder.Build();

if (bootstrapMinistryAdmin)
{
    await MinistryAdminBootstrap.RunAsync(app.Services);
    return;
}

await LocalDemoUserSeeder.SeedAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();//API đọc cookie đăng nhập
app.UseAuthorization();//chặn API khi người dùng không có quyền
app.UseAntiforgery();
app.UseDefaultFiles();
app.UseStaticFiles();

// A liveness probe only: it must not imply database, worker or AWS readiness.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapInfrastructureHealth();
app.MapJobEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/health/db", async (AiExamBankDbContext db) =>
        await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "connected" })
            : Results.StatusCode(503));
}

app.MapGet("/auth/me", async (
    HttpContext context,
    UserManager<AppUser> userManager,
    AiExamBankDbContext db) =>
{
    var user = await userManager.GetUserAsync(context.User);
    if (user is null)
        return Results.Unauthorized();

    var assignments = await db.RoleAssignments.AsNoTracking()
        .Where(assignment => assignment.UserId == user.Id)
        .ToListAsync();

    return Results.Ok(new
    {
        user.Id,
        user.UserName,
        roles = assignments.Select(assignment => new
        {
            role = assignment.Role.ToString(),
            assignment.SchoolId,
            assignment.DepartmentId
        })
    });
})
    .RequireAuthorization();

app.MapGet("/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
    TypedResults.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));

app.MapRoleAdministrationEndpoints();
app.MapSchoolAdministrationEndpoints();
app.MapUserAdministrationEndpoints();

app.MapPost("/auth/login", async (
    LoginRequest request,
    SignInManager<AppUser> signInManager,
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest();

    if (string.IsNullOrWhiteSpace(request.UserName) ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest();

    var result = await signInManager.PasswordSignInAsync(
        request.UserName,
        request.Password,
        isPersistent: false,
        lockoutOnFailure: true);

    return result.Succeeded
        ? Results.NoContent()
        : Results.Unauthorized();
}).AllowAnonymous().RequireRateLimiting("login");

app.MapPost("/auth/logout", async (
    SignInManager<AppUser> signInManager,
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest();

    await signInManager.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.Run();
