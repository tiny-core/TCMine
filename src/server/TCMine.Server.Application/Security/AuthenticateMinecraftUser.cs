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

                // Sem e-mail e sem hash de senha de propósito: esta conta só
                // entra pelo launcher. Um PasswordHash nulo é justamente o que
                // faz o login local recusá-la.
                LastSeenAt = DateTimeOffset.UtcNow
            };

            await users.AddAsync(user, ct);
            return Result<User>.Success(user);
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
