using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Infrastructure.Storage;

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

        return services;
    }
}