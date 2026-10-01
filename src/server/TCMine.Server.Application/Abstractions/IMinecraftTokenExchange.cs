using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Porta para a cadeia Xbox Live → XSTS → Minecraft Services, vista da
///     Application — a implementação (em Infrastructure) é um adaptador fino
///     sobre o serviço compartilhado <c>TCMine.MinecraftAuth</c>, o mesmo que o
///     launcher usa. Existe como porta, e não como dependência direta do
///     pacote compartilhado, para manter <see cref="Security.AuthenticateMicrosoftUser" />
///     testável com um fake, como todo o resto da Application (ver
///     <c>AuthenticateMicrosoftUser</c>, em <c>Security/</c>).
/// </summary>
public interface IMinecraftTokenExchange
{
    Task<Result<string>> ExchangeAsync(string microsoftAccessToken, CancellationToken ct);
}
