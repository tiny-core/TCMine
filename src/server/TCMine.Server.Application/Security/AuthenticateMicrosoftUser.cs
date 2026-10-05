using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Common;
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
    IActivityLogRepository activity,
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
            // Conta do painel ainda sem Minecraft: tenta vincular agora. Sem
            // isto, quem entrou no painel primeiro (e a Mojang não respondeu, ou
            // o jogo foi comprado depois) ganhava uma SEGUNDA conta no primeiro
            // login pelo launcher — e as duas nunca mais se encontravam.
            if (user.MinecraftUuid is null)
                await AdoptMinecraftAsync(user, microsoft.AccessToken, ct);

            user.DisplayName = microsoft.DisplayName;
            user.LastSeenAt = now;
            await users.UpdateAsync(user, ct);
            return await SuccessAsync(user, ct);
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
                return await SuccessAsync(adopted, ct);
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

        if (await users.TryAddAsync(user, ct))
            return await SuccessAsync(user, ct);

        // Corrida com outro login da mesma conta: adota a linha que venceu.
        var winner = await users.GetByMicrosoftObjectIdAsync(microsoft.ObjectId, ct);
        return winner is not null
            ? await SuccessAsync(winner, ct)
            : Result<User>.Fail("Não foi possível registrar a conta. Tente de novo.");
    }

    /// <summary>
    ///     Login no feed de atividade — só o do painel (Microsoft). O do launcher
    ///     (<see cref="AuthenticateMinecraftUser" />) roda a cada abertura, com
    ///     reautenticação silenciosa a cada sessão; registrar isso ali inundaria
    ///     o feed com entradas de jogador sem o mesmo valor informativo.
    /// </summary>
    private async Task<Result<User>> SuccessAsync(User user, CancellationToken ct)
    {
        await activity.AddAsync(new ActivityEvent
        {
            Kind = ActivityEventKind.UserLoggedIn,
            Message = $"{user.DisplayName} entrou no painel.",
            Href = "/admin/users"
        }, ct);

        return Result<User>.Success(user);
    }

    /// <summary>
    ///     Vincula o Minecraft a uma conta do painel que ainda não o tinha. Se o
    ///     launcher já criou uma conta para esse jogador (só com o UUID, sem
    ///     Microsoft), ela é a MESMA pessoa e é fundida nesta. Uma conta que tem
    ///     outra Microsoft fica intocada: aí são pessoas diferentes disputando o
    ///     jogo, e quem resolve é o admin, não um login.
    /// </summary>
    private async Task AdoptMinecraftAsync(User user, string microsoftAccessToken, CancellationToken ct)
    {
        var minecraftUuid = await TryResolveMinecraftUuidAsync(microsoftAccessToken, ct);
        if (minecraftUuid is null)
            return;

        var other = await users.GetByMinecraftUuidAsync(minecraftUuid, ct);
        if (other is not null && other.Id != user.Id)
        {
            if (other.MicrosoftObjectId is not null)
                return;

            await users.MergeAsync(user.Id, other.Id, ct);
        }

        user.MinecraftUuid = minecraftUuid;
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
