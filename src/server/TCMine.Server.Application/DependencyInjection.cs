using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Servers;

namespace TCMine.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTcMineApplication(this IServiceCollection services)
    {
        // Casos de uso são scoped: um por requisição, alinhado com o tempo
        // de vida do DbContext.
        services.AddScoped<CreateModpack>();
        services.AddScoped<CreateModpackVersion>();

        services.AddScoped<AddManualFile>();

        services.AddScoped<ModpackIngestionService>();
        services.AddScoped<QueueIngestion>();

        services.AddScoped<RemoveModpackFile>();

        services.AddScoped<PublishModpackVersion>();
        services.AddScoped<CheckModpackVersionUpdates>();
        services.AddScoped<CloneVersion>();

        services.AddScoped<ReadOverride>();
        services.AddScoped<SaveOverride>();
        services.AddScoped<DeleteOverride>();

        services.AddScoped<CreateNews>();
        services.AddScoped<UpdateNews>();
        services.AddScoped<DeleteNews>();

        services.AddScoped<CreateGameServer>();
        services.AddScoped<UpdateGameServer>();
        services.AddScoped<DeleteGameServer>();

        return services;
    }
}