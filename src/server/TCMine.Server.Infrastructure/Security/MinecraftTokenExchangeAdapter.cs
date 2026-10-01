using TCMine.MinecraftAuth;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Infrastructure.Security;

/// <summary>
///     Adaptador fino sobre o <see cref="MinecraftTokenExchange" /> compartilhado
///     — o mesmo que o launcher usa. Existe só para a Application enxergar a
///     porta <see cref="IMinecraftTokenExchange" />, e não o pacote
///     compartilhado direto; ver a nota na porta.
/// </summary>
public sealed class MinecraftTokenExchangeAdapter(MinecraftTokenExchange exchange) : IMinecraftTokenExchange
{
    public async Task<Result<string>> ExchangeAsync(string microsoftAccessToken, CancellationToken ct)
    {
        var result = await exchange.ExchangeAsync(microsoftAccessToken, ct);

        return result.Outcome is MinecraftTokenOutcome.Success
            ? Result<string>.Success(result.AccessToken!)
            : Result<string>.Fail(result.Message!);
    }
}
