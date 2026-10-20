using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Players;

/// <summary>
///     Grava as opções do jogador neste modpack — o launcher chama ao fechar o
///     jogo. Vale a gravação mais recente: entre dois PCs, a do último jogo
///     fechado.
/// </summary>
public sealed class SavePlayerModpackSettings(
    IPlayerSettingsRepository settings,
    IModpackRepository modpacks,
    ICurrentUserScope scope)
{
    public async Task<Result> HandleAsync(Guid modpackId, string keyBindings, string? options, CancellationToken ct)
    {
        if (scope.UserId is not { } userId)
            return Result.Fail("Entre na sua conta para gravar as suas opções.");

        if (keyBindings.Length > PlayerModpackSettings.MaxKeyBindingsLength)
            return Result.Fail("As teclas enviadas passam do tamanho aceito.");

        if (options?.Length > PlayerModpackSettings.MaxOptionsLength)
            return Result.Fail("As opções enviadas passam do tamanho aceito.");

        // A chave estrangeira recusaria na mesma, mas com uma exceção de banco
        // em vez de uma mensagem.
        if (await modpacks.GetHeaderAsync(modpackId, ct) is null)
            return Result.Fail("Modpack não encontrado.");

        var existing = await settings.GetAsync(userId, modpackId, ct);

        if (existing is null)
        {
            var created = new PlayerModpackSettings { UserId = userId, ModpackId = modpackId };
            created.Replace(keyBindings, options);
            await settings.AddAsync(created, ct);
        }
        else
        {
            existing.Replace(keyBindings, options);
            await settings.UpdateAsync(existing, ct);
        }

        return Result.Success();
    }
}
