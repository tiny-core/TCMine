using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

/// <summary>
///     "Nova versão" e "Publicar" são chamados de mais de um lugar — a Visão
///     geral e as ações rápidas do cabeçalho do workspace (Mods, Recursos,
///     Overrides) —, e os dois precisam do mesmo texto. Mesma ideia do
///     ServerActions: o caso de uso e o aviso moram aqui.
///     Diferente do ServerActions, a CONFIRMAÇÃO de publicar também entra:
///     o texto muda com quantos mods faltam e com os nomes deles, então não é
///     um "tem certeza?" genérico que vale repetir em cada chamador — é regra
///     de negócio, e duas cópias dela discordariam no primeiro ajuste.
/// </summary>
public sealed class VersionLifecycleActions(
    ISettingsRepository settings,
    PublishModpackVersion publishUseCase,
    IDialogService dialogService,
    ISnackbar snackbar)
{
    /// <summary>
    ///     Abre "Nova versão" com os padrões herdados da última versão (ou da
    ///     instalação, se não houver uma de quem herdar). Devolve se criou —
    ///     quem chama decide como recarregar.
    /// </summary>
    /// <param name="preferredSourceId">
    ///     A versão que o admin está a ver, quando há uma. Se for publicada, é
    ///     ela que vem marcada como origem — "nova versão a partir desta" é o
    ///     gesto natural de quem está olhando uma versão específica.
    /// </param>
    public async Task<bool> OpenCreateVersionAsync(Modpack modpack, CancellationToken ct, Guid? preferredSourceId = null)
    {
        var latest = modpack.Versions.OrderByDescending(v => v.Id).FirstOrDefault();

        // Só publicadas servem de origem: um rascunho ainda pode mudar, e uma
        // versão que falhou nunca teve os arquivos completos.
        var published = modpack.Versions
            .Where(v => v.State is ModpackVersionState.Ready or ModpackVersionState.Archived)
            .OrderByDescending(v => v.Id)
            .Select(v => new SourceVersionOption(
                v.Id,
                v.State is ModpackVersionState.Archived ? $"{v.Version} (arquivada)" : v.Version))
            .ToList();

        var defaultSource = published.Any(p => p.Id == preferredSourceId)
            ? preferredSourceId
            : modpack.Versions
                .Where(v => v.State is ModpackVersionState.Ready)
                .OrderByDescending(v => v.Id)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefault();

        // A versão anterior manda mais que o padrão da instalação: se este pack
        // já rodou com 8 GB, repetir 4 GB do padrão seria um passo atrás. O
        // padrão só entra quando não há de quem herdar.
        var installationSettings = await settings.GetAsync(ct);
        var memoriaPadrao = latest?.RecommendedMemoryMb ?? installationSettings.DefaultMemoryMb;

        var parameters = new DialogParameters
        {
            ["ModpackId"] = modpack.Id,
            ["DefaultVersion"] = latest?.Version,
            ["MinecraftVersion"] = modpack.MinecraftVersion,
            ["Loader"] = modpack.Loader,
            ["DefaultLoaderVersion"] = latest?.LoaderVersion,
            ["DefaultMemoryMb"] = memoriaPadrao,
            ["SourceVersions"] = published,
            ["DefaultSourceVersionId"] = defaultSource
        };

        var dialog = await dialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);
        return await dialog.Result is { Canceled: false };
    }

    /// <summary>
    ///     Confirma — com pendências, o texto nomeia os mods que não vieram — e
    ///     publica. Devolve se publicou.
    /// </summary>
    public async Task<bool> PublishWithConfirmAsync(ModpackVersion version, CancellationToken ct)
    {
        var pending = version.PendingMods;
        var message = pending.Count > 0
            ? $"A versão {version.Version} tem {pending.Count} mod(s) que não vieram: "
              + $"{string.Join(", ", pending.Take(5).Select(p => p.DisplayName))}"
              + (pending.Count > 5 ? "…" : "")
              + ". Quem instalar não terá esses mods. Publicar assim mesmo?"
            : $"Publicar a versão {version.Version}? A partir daqui ela fica imutável — "
              + "para mudanças, cria uma nova versão.";

        var confirm = await dialogService.ShowMessageBoxAsync(
            pending.Count > 0 ? "Publicar com mods faltando" : "Publicar versão",
            message,
            pending.Count > 0 ? "Publicar mesmo assim" : "Publicar",
            cancelText: "Cancelar");
        if (confirm is not true)
            return false;

        var result = await publishUseCase.HandleAsync(version.Id, ct, pending.Count > 0);
        if (!result.Succeeded)
        {
            snackbar.Add(result.Error!, Severity.Error);
            return false;
        }

        snackbar.Add("Versão publicada.", Severity.Success);
        return true;
    }
}

/// <summary>Uma versão publicada que pode servir de origem para a próxima.</summary>
public sealed record SourceVersionOption(Guid Id, string Label);
