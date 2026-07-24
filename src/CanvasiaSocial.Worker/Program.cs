using CanvasiaSocial.Application;
using CanvasiaSocial.Infrastructure;
using CanvasiaSocial.Infrastructure.Canvasia;
using CanvasiaSocial.Infrastructure.Synchronization;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Infrastructure.Jobs;
using CanvasiaSocial.Infrastructure.Health;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplicationServices();
builder.Services.AddSharedDataProtection(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);
var generalWorkerCount = Math.Clamp(builder.Configuration.GetValue("GENERAL_WORKER_COUNT", 4), 1, 32);
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"canvasia-social-general-{Environment.MachineName}";
    options.WorkerCount = generalWorkerCount;
    options.Queues = ["campaign", "default"];
});
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"canvasia-social-publish-{Environment.MachineName}";
    options.WorkerCount = 2;
    options.Queues = ["publish"];
});
var campaignOptions = CampaignOptions.FromConfiguration(builder.Configuration);
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"canvasia-social-ai-{Environment.MachineName}";
    options.WorkerCount = campaignOptions.AiBatchConcurrency;
    options.Queues = ["ai"];
});

var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
var canvasiaOptions = app.Services.GetRequiredService<CanvasiaOptions>();
recurringJobs.AddOrUpdate<CanvasiaProductSyncJob>(
    "canvasia-product-sync",
    job => job.ExecuteAsync(CancellationToken.None),
    canvasiaOptions.SyncCron,
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
recurringJobs.AddOrUpdate<RecoverCampaignJobsJob>(
    "recover-campaign-jobs",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/1 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
recurringJobs.AddOrUpdate<DispatchDuePostsJob>(
    "dispatch-due-social-posts",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/1 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
recurringJobs.AddOrUpdate<RecoverPublishingPostsJob>(
    "recover-interrupted-social-posts",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
recurringJobs.AddOrUpdate<CleanupOAuthStatesJob>(
    "cleanup-oauth-states",
    job => job.ExecuteAsync(CancellationToken.None),
    "15 3 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
await app.RunAsync();
