using TCMine.Server.Application.Servers;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Registra quais servidores tiveram a whitelist sincronizada.
///     Fake e não a classe real: sincronizar de verdade pede RCON e repositório
///     de servidor, e um teste de convite não deveria montar nada disso para
///     verificar que o convite foi resgatado.
/// </summary>
public sealed class FakeWhitelistSync : IServerWhitelistSync
{
    public List<Guid> Sincronizados { get; } = [];

    public Task HandleAsync(Guid gameServerId, CancellationToken ct)
    {
        Sincronizados.Add(gameServerId);
        return Task.CompletedTask;
    }
}
