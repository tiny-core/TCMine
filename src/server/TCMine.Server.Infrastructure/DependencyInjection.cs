using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Infrastructure.Docker;
using TCMine.Server.Infrastructure.Ingestion;
using TCMine.Server.Infrastructure.Ingestion.CurseForge;
using TCMine.Server.Infrastructure.Ingestion.Modrinth;
using TCMine.Server.Infrastructure.Instances;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Infrastructure.Security;
using TCMine.Server.Infrastructure.Storage;
using TCMine.Server.Infrastructure.Versions;

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

        // O Modrinth pede um User-Agent identificável — a API rejeita
        // requisições sem ele. A convenção deles é "nome/versão (contato)".
        services.AddHttpClient<IModResolver, ModrinthModResolver>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        // HttpClient nomeado para o download dos mods durante a ingestão.
        services.AddScoped<ModpackIngestionService>();

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

        services.AddHttpClient<IModDownloader, HttpModDownloader>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine/1.0 (github.com/tiny-core/TCMine)");
            })
            .AddStandardResilienceHandler();

        services.AddScoped<INewsRepository, NewsRepository>();

        services.AddScoped<IServerRepository, ServerRepository>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

        // Sem SMTP ainda: o link de recuperação vai para o log. Trocar por um
        // SmtpEmailSender quando a tela de Configurações existir.
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        services.Configure<DockerOptions>(configuration.GetSection("Docker"));
        services.AddSingleton<DockerHttpClientFactory>();
        services.AddSingleton<DockerApiClient>();

        services.Configure<InstanceOptions>(configuration.GetSection("Instances"));
        services.AddSingleton<IInstanceMaterializer, FileSystemInstanceMaterializer>();

        services.AddScoped<IServerOrchestrator, DockerServerOrchestrator>();

        services.AddMemoryCache();

        services.AddHttpClient<MinecraftVersionSource>();
        services.AddHttpClient<NeoForgeVersionSource>();
        services.AddHttpClient<ForgeVersionSource>();
        services.AddHttpClient<FabricVersionSource>();
        services.AddHttpClient<QuiltVersionSource>();

        services.AddSingleton<IVersionCatalog, VersionCatalog>();

        return services;
    }
}
