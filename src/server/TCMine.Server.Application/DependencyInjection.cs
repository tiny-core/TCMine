using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTcMineApplication(this IServiceCollection services)
    {
        // Casos de uso são scoped: um por requisição, alinhado com o tempo
        // de vida do DbContext.
        services.AddScoped<CreateModpack>();

        return services;
    }
}