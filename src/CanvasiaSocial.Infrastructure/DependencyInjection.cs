using CanvasiaSocial.Infrastructure.Identity;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Application.Dashboard;
using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Application.Synchronization;
using CanvasiaSocial.Infrastructure.Canvasia;
using CanvasiaSocial.Infrastructure.Dashboard;
using CanvasiaSocial.Infrastructure.Products;
using CanvasiaSocial.Infrastructure.Synchronization;
using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Infrastructure.Ai;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Infrastructure.Social;
using CanvasiaSocial.Domain.Enums;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanvasiaSocial.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICanvasiaProductMapper, CanvasiaProductMapper>();
        services.AddSingleton<ICanvasiaConfigurationService, CanvasiaConfigurationService>();
        services.AddScoped<IProductCacheService, ProductCacheService>();
        services.AddScoped<ICanvasiaProductSyncService, CanvasiaProductSyncService>();
        services.AddScoped<ISingleContentService, SingleContentService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IDraftService, DraftService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddSingleton<IScheduleCalculator, ScheduleCalculator>();
        services.AddScoped<ISocialAccountService, SocialAccountService>();
        services.AddSingleton<ISocialTokenProtector, SocialTokenProtector>();

        var campaignOptions = CampaignOptions.FromConfiguration(configuration);
        services.AddSingleton(campaignOptions);

        var socialProviderOptions = SocialProviderOptions.FromConfiguration(configuration);
        services.AddSingleton(socialProviderOptions);
        services.AddHttpClient<InstagramPublisher>(client => client.Timeout = TimeSpan.FromSeconds(120))
            .RedactLoggedHeaders(["Authorization"]);
        services.AddHttpClient<FacebookPublisher>(client => client.Timeout = TimeSpan.FromSeconds(120))
            .RedactLoggedHeaders(["Authorization"]);
        services.AddTransient<TikTokPublisher>();
        services.AddTransient<PinterestPublisher>();
        services.AddTransient<ISocialPublisher>(provider => provider.GetRequiredService<InstagramPublisher>());
        services.AddTransient<ISocialPublisher>(provider => provider.GetRequiredService<FacebookPublisher>());
        services.AddTransient<ISocialPublisher>(provider => provider.GetRequiredService<TikTokPublisher>());
        services.AddTransient<ISocialPublisher>(provider => provider.GetRequiredService<PinterestPublisher>());

        var imageOptions = SecureImageOptions.FromConfiguration(configuration);
        services.AddSingleton(imageOptions);
        services.AddScoped<ISecureImageService, SecureImageService>();

        var openRouterOptions = OpenRouterOptions.FromConfiguration(configuration);
        services.AddSingleton(openRouterOptions);
        services.AddTransient<OpenRouterAuthHandler>();
        services.AddHttpClient<IAiContentGenerator, OpenRouterContentGenerator>(client =>
            {
                client.BaseAddress = new Uri(openRouterOptions.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(openRouterOptions.TimeoutSeconds);
            })
            .AddHttpMessageHandler<OpenRouterAuthHandler>()
            .RedactLoggedHeaders([OpenRouterAuthHandler.AuthorizationHeader]);

        var canvasiaOptions = CanvasiaOptions.FromConfiguration(configuration);
        services.AddSingleton(canvasiaOptions);
        services.AddTransient<CanvasiaApiKeyHandler>();
        services.AddTransient<CanvasiaResilienceHandler>();
        services.AddHttpClient<ICanvasiaApiClient, CanvasiaApiClient>((_, client) =>
            {
                if (canvasiaOptions.HasValidBaseUrl)
                {
                    client.BaseAddress = new Uri(canvasiaOptions.BaseUrl.TrimEnd('/') + "/");
                }
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<CanvasiaApiKeyHandler>()
            .AddHttpMessageHandler<CanvasiaResilienceHandler>()
            .RedactLoggedHeaders([CanvasiaApiKeyHandler.HeaderName]);

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true
                }));

        return services;
    }

    public static IServiceCollection AddSharedDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var path = configuration["DataProtection:KeysPath"] ?? configuration["DATA_PROTECTION_KEYS_PATH"] ?? "./keys";
        var builder = services.AddDataProtection().SetApplicationName("CanvasiaSocial");
        if (string.Equals(configuration["DATA_PROTECTION_STORE"], "database", StringComparison.OrdinalIgnoreCase))
            builder.PersistKeysToDbContext<ApplicationDbContext>();
        else
            builder.PersistKeysToFileSystem(new DirectoryInfo(path));
        return services;
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection bağlantı dizesi yapılandırılmamış.");
}
