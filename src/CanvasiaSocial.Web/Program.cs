using CanvasiaSocial.Application;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Infrastructure;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Infrastructure.Health;
using CanvasiaSocial.Web.Security;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplicationServices();
builder.Services.AddSharedDataProtection(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
var useHttpsRedirection = builder.Configuration.GetValue("Security:UseHttpsRedirection", true);
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<CanvasiaApiHealthCheck>("canvasia-api", tags: ["ready"]);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "CanvasiaSocial.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = useHttpsRedirection
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ApplicationPolicies.ViewDashboard,
        policy => policy.RequireRole(ApplicationRoles.All));
    options.AddPolicy(ApplicationPolicies.ManageContent,
        policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.Editor));
    options.AddPolicy(ApplicationPolicies.ApproveContent,
        policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.Approver));
    options.AddPolicy(ApplicationPolicies.ManageUsers,
        policy => policy.RequireRole(ApplicationRoles.Admin));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "CanvasiaSocial.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = useHttpsRedirection
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/giris";
    options.AccessDeniedPath = "/erisim-reddedildi";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var loginRateLimit = Math.Clamp(builder.Configuration.GetValue("LOGIN_RATE_LIMIT_PER_MINUTE", 10), 5, 100);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginRateLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync(
            "Çok fazla giriş denemesi yapıldı. Lütfen bir dakika sonra tekrar deneyin.",
            cancellationToken);
    };
});

var trustForwardedHeaders = builder.Configuration.GetValue("TRUST_FORWARDED_HEADERS", false);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

var app = builder.Build();
if (trustForwardedHeaders) app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} {StatusCode} in {Elapsed:0.0000} ms";
});
if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter()]
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
var runMigrations = migrateOnly || builder.Configuration.GetValue("RUN_MIGRATIONS", true);
if (runMigrations) await app.Services.MigrateAndSeedIdentityAsync();
if (migrateOnly) return;
await app.RunAsync();

public partial class Program;
