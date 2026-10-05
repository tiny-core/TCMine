using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Login do jogador pelo launcher, a partir do token de acesso do Minecraft.
///     Diferente do login local, aqui não existe cadastro prévio: quem prova ser
///     dono de uma conta Minecraft ganha um <see cref="User" /> na hora. Isso não
///     dá acesso a nada — sem <see cref="Membership" />, o jogador não enxerga
///     servidor nenhum. Separar as duas coisas é o que permite convidar alguém
///     pelo nome de jogador antes de ele ter entrado a primeira vez.
/// </summary>
public sealed class AuthenticateMinecraftUser(
    IUserRepository users,
    IMinecraftProfileSource profiles)
{
    public async Task<Result<User>> HandleAsync(string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return Result<User>.Fail("Token de acesso ausente.");

        var profile = await profiles.GetProfileAsync(accessToken, ct);
        if (profile is null)
            return Result<User>.Fail("Conta Minecraft não verificada.");

        var user = await users.GetByMinecraftUuidAsync(profile.Uuid, ct);

        if (user is null)
        {
            user = new User
            {
                DisplayName = profile.Name,
                MinecraftUuid = profile.Uuid,

                // Sem MicrosoftObjectId de propósito: o launcher manda só o
                // token do Minecraft, nunca o token Microsoft que traria o oid.
                // Se esta mesma pessoa já tem (ou um dia tiver) conta no painel
                // pela Microsoft, o login de lá resolve o Minecraft e FUNDE esta
                // conta na dele (AuthenticateMicrosoftUser/LinkMinecraftAccount).
                LastSeenAt = DateTimeOffset.UtcNow
            };

            if (await users.TryAddAsync(user, ct))
                return Result<User>.Success(user);

            // Outra requisição criou esta mesma conta entre a busca e a
            // gravação — o launcher faz isso no primeiro arranque, quando o
            // login silencioso e o da tela correm juntos. O índice único
            // segurou a segunda linha; aqui adotamos a que venceu, em vez de
            // devolver erro a quem só estava entrando.
            user = await users.GetByMinecraftUuidAsync(profile.Uuid, ct);
            if (user is null)
                return Result<User>.Fail("Não foi possível registrar a conta. Tente de novo.");
        }

        // O nome de jogador pode ser trocado a cada 30 dias. Reconhecemos a
        // pessoa pelo UUID e trazemos o nome novo junto, senão o painel exibiria
        // para sempre o apelido que ela tinha no primeiro login.
        user.DisplayName = profile.Name;
        user.LastSeenAt = DateTimeOffset.UtcNow;
        await users.UpdateAsync(user, ct);

        return Result<User>.Success(user);
    }
}
