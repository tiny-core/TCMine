using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Storage;
using TCMine.Server.Application.Settings;

namespace TCMine.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTcMineApplication(this IServiceCollection services)
    {
        // Casos de uso são scoped: um por requisição, alinhado com o tempo
        // de vida do DbContext.
        services.AddScoped<CreateModpack>();
        services.AddScoped<DeleteModpack>();
        services.AddScoped<CreateModpackVersion>();

        services.AddScoped<AddManualFile>();

        services.AddScoped<ModpackIngestionService>();
        services.AddScoped<QueueIngestion>();

        // Ponto unico de entrada da fila: grava o pedido antes de enfileirar.
        services.AddScoped<IngestionScheduler>();
        services.AddScoped<RecoverInterruptedIngestions>();
        services.AddScoped<ImportScheduler>();
        services.AddScoped<RecoverInterruptedImports>();

        services.AddScoped<RemoveModpackFile>();

        services.AddScoped<PublishModpackVersion>();
        services.AddScoped<CheckModpackVersionUpdates>();
        services.AddScoped<UpdateModpackVersion>();
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

        services.AddScoped<ChangeServerVersion>();

        services.AddScoped<CreateWorldBackup>();
        services.AddScoped<RestoreWorldBackup>();
        services.AddScoped<DeleteWorldBackup>();

        services.AddScoped<StartGameServer>();
        services.AddScoped<StopGameServer>();
        services.AddScoped<UpdateModpack>();
        services.AddScoped<SetModpackIcon>();

        services.AddSingleton<OverrideUndoService>();
        services.AddScoped<MoveOverride>();
        services.AddScoped<UndoOverrideMove>();

        services.AddScoped<DeleteModpackVersion>();
        services.AddScoped<ArchiveModpackVersion>();
        services.AddScoped<RestoreModpackVersion>();

        services.AddScoped<AuthenticateUser>();
        services.AddScoped<AuthenticateMinecraftUser>();
        services.AddScoped<CreateInvite>();
        services.AddScoped<RedeemInvite>();
        services.AddScoped<RevokeInvite>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<ChangeMemberRole>();
        services.AddScoped<CreateFirstAdmin>();
        services.AddScoped<ChangePassword>();
        services.AddScoped<UpdateSettings>();
        services.AddScoped<ImportUpstreamPack>();
        services.AddScoped<RetryModResolution>();
        services.AddScoped<CheckUpstreamUpdate>();
        services.AddScoped<UpdateFromUpstream>();

        services.AddScoped<ScanStorage>();
        services.AddScoped<DeleteOrphanBlobs>();
        services.AddScoped<RequestPasswordReset>();
        services.AddScoped<ResetPassword>();

        return services;
    }
}
