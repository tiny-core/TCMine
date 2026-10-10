using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Public;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Settings;
using TCMine.Server.Application.Storage;
using TCMine.Server.Application.Updates;

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

        services.AddScoped<ListModpackAccess>();
        services.AddScoped<AddModpackEditor>();
        services.AddScoped<RemoveModpackEditor>();

        services.AddScoped<GetPublicCatalog>();

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

        // Nuvem de itens (mod tccloud). TimeProvider em vez de UtcNow direto: a
        // expiração do lease é regra de negócio e os testes precisam avançar o
        // relógio. TryAdd para um teste poder registrar o relógio falso antes.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuthenticateCloudServer>();
        services.AddScoped<ProvisionServerCloudKey>();
        services.AddScoped<CloudHello>();
        services.AddScoped<AcquireCloudLease>();
        services.AddScoped<HeartbeatCloudLeases>();
        services.AddScoped<SubmitCloudBatch>();
        services.AddScoped<ReleaseCloudLease>();
        services.AddScoped<ReportCloudDoubtful>();
        services.AddScoped<ReportCloudSuspects>();
        services.AddScoped<GetCloudPolicy>();
        services.AddScoped<ExtendCloudLeasesAfterOutage>();
        services.AddScoped<ListCloudVaults>();
        services.AddScoped<CreateCloudVault>();
        services.AddScoped<GetCloudVault>();
        services.AddScoped<UpdateCloudVault>();
        services.AddScoped<ListCloudVaultServers>();
        services.AddScoped<SetServerCloudVault>();
        services.AddScoped<RevokeCloudServerKey>();
        services.AddScoped<ListCloudPlayers>();
        services.AddScoped<GetCloudChannelBalances>();
        services.AddScoped<UnfreezeCloudChannel>();
        services.AddScoped<ForceReleaseCloudLease>();
        services.AddScoped<ListCloudRules>();
        services.AddScoped<AddCloudRule>();
        services.AddScoped<RemoveCloudRule>();
        services.AddScoped<ListCloudSuspects>();
        services.AddScoped<ResolveCloudSuspect>();
        services.AddScoped<ListCloudQuarantine>();
        services.AddScoped<ResolveCloudQuarantine>();
        services.AddScoped<ListCloudDoubtful>();
        services.AddScoped<ResolveCloudDoubtful>();
        services.AddScoped<ListCloudIncidents>();
        services.AddScoped<ResolveCloudIncident>();
        services.AddScoped<ListCloudAudit>();

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

        services.AddScoped<AuthenticateMicrosoftUser>();
        services.AddScoped<AuthenticateMinecraftUser>();
        services.AddScoped<LinkMinecraftAccount>();

        // Singleton: o código emitido no arranque precisa ser o mesmo que o
        // resgate confere, em qualquer requisição.
        services.AddSingleton<AdminClaimCode>();
        services.AddScoped<ClaimInstanceAdmin>();
        services.AddScoped<CheckServerUpdate>();
        services.AddScoped<CreateInvite>();
        services.AddScoped<ListServerAccess>();
        services.AddScoped<ListAccessibleServers>();
        services.AddScoped<SendServerCommand>();
        services.AddScoped<RedeemInvite>();
        services.AddScoped<RevokeInvite>();
        services.AddScoped<ListUsers>();
        services.AddScoped<SetInstanceAdmin>();
        services.AddScoped<RequestServerAccess>();
        services.AddScoped<ListAccessRequests>();
        services.AddScoped<ApproveAccessRequest>();
        services.AddScoped<DenyAccessRequest>();
        services.AddScoped<RemoveMember>();
        services.AddScoped<ChangeMemberRole>();
        services.AddScoped<UpdateSettings>();
        services.AddScoped<GetPublicHost>();
        services.AddScoped<ImportUpstreamPack>();
        services.AddScoped<CompleteFromServerPack>();
        services.AddScoped<BackfillServerPacks>();
        services.AddScoped<ChangeFileSide>();
        services.AddScoped<IServerWhitelistSync, SyncServerWhitelist>();
        services.AddScoped<RetryModResolution>();
        services.AddScoped<CheckUpstreamUpdate>();
        services.AddScoped<UpdateFromUpstream>();

        services.AddScoped<ScanStorage>();
        services.AddScoped<DeleteOrphanBlobs>();

        return services;
    }
}
