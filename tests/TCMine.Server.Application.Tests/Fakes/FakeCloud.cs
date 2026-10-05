using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Arquivo da chave em memória: guarda o último conteúdo por servidor.</summary>
internal sealed class FakeCloudServerFiles : ICloudServerFiles
{
    public Dictionary<Guid, (Uri Url, string Key)> Files { get; } = [];

    /// <summary>Simula disco cheio/sem permissão.</summary>
    public bool Fail { get; init; }

    public Task WriteAsync(Guid gameServerId, Uri cloudUrl, string key, CancellationToken ct)
    {
        if (Fail) throw new IOException("disco cheio");
        Files[gameServerId] = (cloudUrl, key);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid gameServerId, CancellationToken ct)
    {
        if (Fail) throw new IOException("disco cheio");
        Files.Remove(gameServerId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCloudEndpoint(Uri? url) : ICloudEndpointSource
{
    public Uri? ServerFacingUrl => url;
}

internal static class FakeCloud
{
    /// <summary>Provisionador para testes que não exercitam a nuvem (servidor sem nuvem: só apaga o arquivo).</summary>
    public static ProvisionServerCloudKey Provisioner(IServerRepository servers, FakeCloudServerFiles? files = null,
        FakeCloudCredentialRepository? credentials = null, Uri? url = null) =>
        new(servers, credentials ?? new FakeCloudCredentialRepository(), files ?? new FakeCloudServerFiles(),
            new FakeCloudEndpoint(url), TimeProvider.System, NullLogger<ProvisionServerCloudKey>.Instance);
}
