using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Public;
using TCMine.Server.Infrastructure.Docker;
using TCMine.Server.Infrastructure.Ingestion;
using TCMine.Server.Infrastructure.Ingestion.CurseForge;
using TCMine.Server.Infrastructure.Ingestion.Modrinth;
using TCMine.Server.Infrastructure.Instances;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Infrastructure.Security;
using TCMine.Server.Infrastructure.Storage;
using TCMine.Server.Infrastructure.Versions;
using TCMine.MinecraftAuth;

namespace TCMine.Server.Infrastructure;

/// <summary>
///     Ponto único de registro da infraestrutura.
///     Concentrar aqui evita que o Program.cs vire uma lista de cinquenta
///     AddScoped — e mantém o detalhe de qual implementação atende, qual porta
///     dentro do projeto que a implementa.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTcMineInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .ValidateOnStart();

        // Singleton porque não guarda estado por requisição e a criação
        // envolve criar diretório — não vale repetir a cada chamada.
        services.AddSingleton<IBlobStore, FileSystemBlobStore>();

        services.AddScoped<IModpackRepository, ModpackRepository>();
        services.AddScoped<IImportRequestRepository, ImportRequestRepository>();

        // O Modrinth pede um User-Agent identificável — a API rejeita
        // requisições sem ele. A convenção deles é "nome/versão (contato)".
        services.AddHttpClient<IModResolver, ModrinthModResolver>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        // ModpackIngestionService é caso de uso (mora em Application/Modpacks) e
        // já é registrado em AddTcMineApplication — registrá-lo aqui também violava
        // a regra de ouro do DI (CLAUDE.md §2): duplicata inofensiva hoje (o
        // último registro por tipo concreto vence), mas raiz clássica de bug se um
        // dia só um dos dois for atualizado.

        services.AddHttpClient<IModSearch, ModrinthModSearch>(client =>
            {
                client.BaseAddress = new Uri("https://api.modrinth.com");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        // ---- CurseForge ----
        // A chave da API não entra aqui: ela vive na configuração da instalação
        // e é lida a cada chamada, para trocá-la pelo painel valer na hora.
        services.AddHttpClient<CurseForgeApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.curseforge.com");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        // Registro por origem: a ingestão e a busca recebem IEnumerable<> e
        // escolhem a implementação pela Origin de cada item.
        services.AddScoped<IModResolver, CurseForgeModResolver>();
        services.AddScoped<IModSearch, CurseForgeModSearch>();

        // Importação de packs inteiros (zip + manifest + overrides).
        services.AddHttpClient<IUpstreamPackSource, CurseForgePackSource>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IUpstreamPackSource, ModrinthPackSource>(client =>
            {
                client.BaseAddress = new Uri("https://api.modrinth.com");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        services.AddHttpClient<IModDownloader, HttpModDownloader>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        services.AddScoped<INewsRepository, NewsRepository>();

        services.AddScoped<IServerRepository, ServerRepository>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
        services.AddScoped<ICloudCredentialRepository, CloudCredentialRepository>();
        services.AddScoped<ICloudStorageRepository, CloudStorageRepository>();
        services.AddScoped<IModpackMembershipRepository, ModpackMembershipRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        // Verificação do login vindo do launcher. Resiliência padrão porque uma
        // instabilidade momentânea da Mojang não deve virar "não consigo entrar":
        // o handler tenta de novo antes de a exceção chegar ao jogador.
        services.AddHttpClient<IMinecraftProfileSource, MinecraftServicesProfileSource>(client =>
            {
                client.BaseAddress = new Uri("https://api.minecraftservices.com");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        // Troca do código de autorização pelo token Microsoft (login do painel).
        // Cliente próprio, sem o que quer que o resto do app use para falar com
        // o próprio TCMine — isto fala só com a Microsoft.
        services.AddHttpClient<IMicrosoftOAuthClient, MicrosoftOAuthClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler();

        // Mesma cadeia Xbox Live → XSTS → Minecraft Services que o launcher usa
        // (TCMine.MinecraftAuth, compartilhado) — aqui, para o login opcional do
        // Minecraft no painel.
        services.AddHttpClient<MinecraftTokenExchange>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler();

        services.AddScoped<IMinecraftTokenExchange, MinecraftTokenExchangeAdapter>();

        // O SmtpEmailSender consulta a configuração a cada envio e cai no
        // LoggingEmailSender quando não há SMTP — por isso o de log continua
        // registrado como classe concreta, e não como a porta.
        services.AddSingleton<LoggingEmailSender>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.Configure<DockerOptions>(configuration.GetSection("Docker"));
        services.AddSingleton<DockerHttpClientFactory>();
        services.AddSingleton<DockerApiClient>();

        services.Configure<InstanceOptions>(configuration.GetSection("Instances"));
        services.AddSingleton<IInstanceMaterializer, FileSystemInstanceMaterializer>();

        services.AddScoped<IServerOrchestrator, DockerServerOrchestrator>();
        services.AddScoped<IContainerStats, DockerContainerStats>();
        services.AddScoped<IBlobJanitor, FileSystemBlobJanitor>();
        services.AddScoped<IWorldBackupStore, FileSystemWorldBackupStore>();
        services.AddScoped<IRconClient, DockerRconClient>();
        services.AddScoped<IMailServerOrchestrator, DockerMailServerOrchestrator>();
        services.AddSingleton<IModJarInspector, ZipModJarInspector>();

        services.AddMemoryCache();

        services.AddHttpClient<MinecraftVersionSource>();
        services.AddHttpClient<NeoForgeVersionSource>();
        services.AddHttpClient<ForgeVersionSource>();
        services.AddHttpClient<FabricVersionSource>();
        services.AddHttpClient<QuiltVersionSource>();

        services.AddSingleton<IVersionCatalog, VersionCatalog>();

        // Sem estado próprio — só lê um arquivo a cada chamada — mas singleton
        // como os demais adaptadores sem estado por requisição.
        services.AddSingleton<ILauncherReleaseSource, FileSystemLauncherReleaseSource>();

        return services;
    }
}
