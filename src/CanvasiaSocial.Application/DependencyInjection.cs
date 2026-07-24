using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CanvasiaSocial.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        return services;
    }
}
