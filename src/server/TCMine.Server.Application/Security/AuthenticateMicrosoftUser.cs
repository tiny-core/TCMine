using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Login do painel pela conta Microsoft — e, ao mesmo tempo, o setup
///     inicial: não há um caso de uso separado para "criar o primeiro admin"
///     porque a regra é a mesma coisa vista de outro ângulo. Antes de criar o
///     usuário, pergunta se já existia algum; se não existia nenhum, este é o
///     primeiro, e o primeiro É o admin da instalação.
///     Diferente do login do launcher (<see cref="AuthenticateMinecraftUser" />),
///     a posse do Minecraft aqui é OPORTUNISTA, não exigida: quem administra o
///     painel pode nunca ter comprado o jogo. Tentamos vincular com o mesmo
///     token Microsoft, e seguimos sem reclamar se não der — a conta nasce só
///     com a identidade Microsoft, e o jogador vincula o Minecraft depois, na
///     tela de conta, se um dia comprar.
/// </summary>
public sealed partial class AuthenticateMicrosoftUser(
    IMicrosoftOAuthClient oauth,
    IMinecraftTokenExchange minecraftExchange,
    IMinecraftProfileSource profiles,
    IUserRepository users,
    ILogger<AuthenticateMicrosoftUser> logger)
{
    public async Task<Result<User>> HandleAsync(
        string azureClientId, string code, string redirectUri, string codeVerifier, CancellationToken ct)
    {
        var identity = await oauth.ExchangeCodeAsync(azureClientId, code, redirectUri, codeVerifier, ct);
        if (!identity.Succeeded)
            return Result<User>.Fail(identity.Error!);

        var microsoft = identity.Value!;
        var now = DateTimeOffset.UtcNow;

        var user = await users.GetByMicrosoftObjectIdAsync(microsoft.ObjectId, ct);

        if (user is not null)
        {
            user.DisplayName = microsoft.DisplayName;
            user.LastSeenAt = now;
            await users.UpdateAsync(user, ct);
            return Result<User>.Success(user);
        }

        // Ninguém com este oid ainda — mas pode já existir uma conta desta MESMA
        // pessoa, criada pelo launcher (que só manda o token do Minecraft, nunca
        // o da Microsoft, e por isso nunca grava o oid). Resolver o Minecraft
        // ANTES de decidir se cria é o que evita duas contas para o mesmo
        // jogador — exatamente o problema que fez o launcher do admin não
        // enxergar o próprio servidor dele.
        var minecraftUuid = await TryResolveMinecraftUuidAsync(microsoft.AccessToken, ct);

        if (minecraftUuid is not null)
        {
            var adopted = await users.GetByMinecraftUuidAsync(minecraftUuid, ct);

            if (adopted is not null)
            {
                adopted.MicrosoftObjectId = microsoft.ObjectId;
                adopted.DisplayName = microsoft.DisplayName;
                adopted.LastSeenAt = now;
                await users.UpdateAsync(adopted, ct);
                return Result<User>.Success(adopted);
            }
        }

        // Perguntado ANTES de criar: depois de AddAsync este usuário já conta
        // como "existe alguém", e a pergunta responderia sempre falso.
        var isFirstUser = !await users.AnyAsync(ct);

        user = new User
        {
            MicrosoftObjectId = microsoft.ObjectId,
            DisplayName = microsoft.DisplayName,
            MinecraftUuid = minecraftUuid,
            IsInstanceAdmin = isFirstUser,
            LastSeenAt = now
        };

        await users.AddAsync(user, ct);
        return Result<User>.Success(user);
    }

    /// <summary>
    ///     Nulo é uma resposta completa aqui, não uma falha: a conta Microsoft
    ///     pode legitimamente não ter o jogo, e tanto a falta de posse quanto
    ///     uma Mojang fora do ar têm o MESMO tratamento certo para um login que
    ///     não depende do Minecraft — seguir sem ele.
    /// </summary>
    private async Task<string?> TryResolveMinecraftUuidAsync(string microsoftAccessToken, CancellationToken ct)
    {
        var minecraftToken = await minecraftExchange.ExchangeAsync(microsoftAccessToken, ct);
        if (!minecraftToken.Succeeded)
            return null;

        try
        {
            var profile = await profiles.GetProfileAsync(minecraftToken.Value!, ct);
            return profile?.Uuid;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMinecraftLinkFailed(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Não foi possível vincular o Minecraft ao logar pela Microsoft; conta segue sem ele.")]
    private partial void LogMinecraftLinkFailed(Exception ex);
}
