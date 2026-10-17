using Microsoft.Extensions.DependencyInjection;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Options;
using TCMine.Launcher.Core.Runtime;

namespace TCMine.Launcher.Core;

public static class DependencyInjection
{
    /// <summary>
    ///     Casos de uso do launcher. Só nomes daqui de dentro: quem implementa as
    ///     portas é registrado por <c>AddLauncherInfrastructure</c>, e esta camada
    ///     nunca vê o nome de uma classe de infraestrutura — a mesma regra do
    ///     <c>AddTCMineApplication</c> do lado do servidor.
    /// </summary>
    public static IServiceCollection AddLauncherCore(this IServiceCollection services)
    {
        services.AddScoped<ServerPairing>();
        services.AddScoped<UpdateLauncher>();
        services.AddScoped<SignIn>();
        services.AddScoped<RedeemInvite>();
        services.AddScoped<LoadCatalog>();
        services.AddScoped<InstallModpackVersion>();

        // A MESMA instância, e não um registo paralelo: resolver a interface por
        // AddScoped<IInstanceInstaller, InstallModpackVersion>() criaria um
        // segundo objeto no mesmo escopo — dois instaladores a partilhar o disco
        // sem saberem um do outro.
        services.AddScoped<IInstanceInstaller>(sp => sp.GetRequiredService<InstallModpackVersion>());
        services.AddScoped<ListInstances>();
        services.AddScoped<ChooseInstance>();
        services.AddScoped<SetInstanceMemory>();
        services.AddScoped<LaunchGame>();
        services.AddScoped<ResumeRunningGame>();
        services.AddScoped<PrepareServerInstance>();
        services.AddScoped<JoinServer>();
        services.AddScoped<UpdateInstance>();
        services.AddScoped<CheckInstanceUpdates>();
        services.AddScoped<CleanupJavaRuntimes>();

        // Singleton: o jogo aberto tem de sobreviver à navegação entre telas, do
        // mesmo modo que o estado do casco.
        services.AddSingleton<GameSession>();

        // Singleton, ao contrário dos outros casos de uso: quem envia as teclas
        // quando o jogo fecha é um coordenador que vive a aplicação inteira, e
        // um singleton não pode depender de algo com escopo. Tudo de que ele
        // depende (canal, disco, preferências) já é singleton.
        services.AddSingleton<SyncPlayerOptions>();

        return services;
    }
}
