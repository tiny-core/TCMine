using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Vincula um Minecraft a uma conta que já existe — o caminho para quem
///     entrou no painel só com a Microsoft (<see cref="AuthenticateMicrosoftUser" />
///     não exige o jogo) e um dia compra o Minecraft. Mesmo token Microsoft,
///     mesma cadeia de troca; a diferença é que aqui falhar é erro de verdade
///     — quem clicou em "vincular" quer saber se não deu certo, ao contrário
///     do login, onde seguir sem Minecraft é uma resposta normal.
/// </summary>
public sealed class LinkMinecraftAccount(
    IMinecraftTokenExchange minecraftExchange,
    IMinecraftProfileSource profiles,
    IUserRepository users)
{
    public async Task<Result> HandleAsync(Guid userId, string microsoftAccessToken, CancellationToken ct)
    {
        var tokenResult = await minecraftExchange.ExchangeAsync(microsoftAccessToken, ct);
        if (!tokenResult.Succeeded)
            return Result.Fail(tokenResult.Error!);

        var profile = await profiles.GetProfileAsync(tokenResult.Value!, ct);
        if (profile is null)
            return Result.Fail("Esta conta Microsoft não tem o Minecraft.");

        // Duas contas não podem reivindicar o mesmo jogador — isso partiria o
        // launcher dele entre as duas, nunca sabendo qual é "a" conta.
        var owner = await users.GetByMinecraftUuidAsync(profile.Uuid, ct);
        if (owner is not null && owner.Id != userId)
        {
            // Conta que só existe porque a pessoa entrou pelo launcher ANTES de
            // vincular o Minecraft aqui: é ela mesma, em duplicata. Provar a
            // posse do jogo agora (o token acima) é a mesma prova que criou a
            // outra — então funde, em vez de prender o jogador numa conta que
            // ele não consegue mais usar no painel.
            if (owner.MicrosoftObjectId is not null)
                return Result.Fail("Esta conta Minecraft já está vinculada a outro usuário.");

            await users.MergeAsync(userId, owner.Id, ct);
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Fail("Usuário não encontrado.");

        user.MinecraftUuid = profile.Uuid;
        await users.UpdateAsync(user, ct);
        return Result.Success();
    }
}
