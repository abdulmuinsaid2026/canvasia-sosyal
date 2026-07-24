using Microsoft.Extensions.Configuration;

namespace CanvasiaSocial.Infrastructure.Ai;

public sealed class OpenRouterOptions
{
    public string BaseUrl { get; init; } = "https://openrouter.ai/api/v1/";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "openai/gpt-4o-mini";
    public int TimeoutSeconds { get; init; } = 120;

    public static OpenRouterOptions FromConfiguration(IConfiguration configuration) => new()
    {
        BaseUrl = configuration["OPENROUTER_BASE_URL"] ?? configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1/",
        ApiKey = configuration["OPENROUTER_API_KEY"] ?? configuration["OpenRouter:ApiKey"] ?? string.Empty,
        Model = configuration["OPENROUTER_MODEL"] ?? configuration["OpenRouter:Model"] ?? "openai/gpt-4o-mini",
        TimeoutSeconds = int.TryParse(configuration["OPENROUTER_REQUEST_TIMEOUT_SECONDS"] ?? configuration["OpenRouter:RequestTimeoutSeconds"], out var timeout)
            ? Math.Clamp(timeout, 10, 300)
            : 120
    };
}
