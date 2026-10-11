using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Confere se a build do loader existe para aquele Minecraft.
///     O formulário já oferece a lista, mas aceita texto digitado — e a borda é
///     plural. Uma build inexistente passava calada até o servidor de jogo
///     tentar subir: o container pedia ao itzg um loader que ninguém publicou, e
///     o erro aparecia num log de container, longe de quem digitou o número.
///     Num lugar só porque criar, editar e publicar uma versão fazem a mesma
///     pergunta, e as três têm de dar a mesma resposta.
/// </summary>
public sealed partial class LoaderVersionGuard(IVersionCatalog catalog, ILogger<LoaderVersionGuard> logger)
{
    private readonly ILogger<LoaderVersionGuard> _logger = logger;

    public async Task<Result> CheckAsync(
        ModLoader loader, string minecraftVersion, string? loaderVersion, CancellationToken ct)
    {
        var wanted = loaderVersion?.Trim() ?? "";

        // Vanilla não tem loader. E build em branco é "a recomendada", decisão
        // que outro ponto do sistema já trata — aqui não há o que conferir.
        if (loader is ModLoader.Vanilla || wanted.Length == 0)
            return Result.Success();

        // Todas, e não só as estáveis: o admin pode ter escolhido uma beta de
        // propósito, com o interruptor "só releases" desligado.
        var lookup = await catalog.GetLoaderVersionsAsync(loader, minecraftVersion, false, ct);

        if (lookup.Failed)
        {
            // Não saber não é motivo para recusar: uma fonte fora do ar não pode
            // impedir ninguém de publicar. Fica no log, porque o que passou sem
            // conferência pode ser justamente o que vai falhar no container.
            LogUnverified(loader.ToString(), wanted, minecraftVersion);
            return Result.Success();
        }

        if (lookup.Versions.Any(known => Matches(loader, known, wanted)))
            return Result.Success();

        return Result.Fail(lookup.Versions.Count == 0
            ? $"Não há {loader} para o Minecraft {minecraftVersion}. Confira o loader e a versão do jogo do modpack."
            : $"A versão {wanted} do {loader} não existe para o Minecraft {minecraftVersion}. "
              + $"Escolha uma da lista; a mais recente é a {lookup.Versions[0]}.");
    }

    /// <summary>
    ///     Igual, sem distinguir maiúsculas. O Forge tem uma tolerância a mais: nas
    ///     versões antigas o maven repete o Minecraft no fim
    ///     ("10.13.4.1614-1.7.10") e os manifestos de pack trazem só a primeira
    ///     parte. Recusar por causa do sufixo impediria publicar um pack importado
    ///     que está certo.
    /// </summary>
    public static bool Matches(ModLoader loader, string known, string wanted) =>
        known.Equals(wanted, StringComparison.OrdinalIgnoreCase)
        || (loader is ModLoader.Forge && known.StartsWith(wanted + "-", StringComparison.OrdinalIgnoreCase));

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Catálogo de versões indisponível: {Loader} {LoaderVersion} para o Minecraft {Minecraft} "
                  + "passou sem conferência.")]
    private partial void LogUnverified(string loader, string loaderVersion, string minecraft);
}
