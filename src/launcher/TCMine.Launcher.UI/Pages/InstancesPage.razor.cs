using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.UI.Pages;

public partial class InstancesPage : ComponentBase
{
    private InstanceKey? _active;
    private bool _busy;
    private string? _busyLabel;

    private IReadOnlyDictionary<InstanceKey, ModpackVersionDto> _updates =
        new Dictionary<InstanceKey, ModpackVersionDto>();
    private IReadOnlyList<InstalledInstance> _instances = [];
    private bool _loading = true;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private CheckInstanceUpdates Updates { get; set; } = default!;

    [Inject] private UpdateInstance Updater { get; set; } = default!;

    [Inject] private InstallModpackVersion Installer { get; set; } = default!;

    [Inject] private IWorldBackup Worlds { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IDesktopShell Desktop { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _instances = await Instances.HandleAsync(CancellationToken.None);

            // A ativa vem resolvida, e não lida em bruto: com uma instância só
            // ela é a ativa sem ninguém ter escolhido, e a etiqueta tem de dizer
            // o mesmo que a tela de jogar mostra.
            _active = (await Active.CurrentAsync(CancellationToken.None)).Active?.Key;

            // Só com servidor: sem ele não há como saber, e "não saber" não é o
            // mesmo que "não há novidade" — mostrar botões que falham ao clicar
            // seria pior do que não mostrar nenhum.
            _updates = Shell.IsOnline
                ? await Updates.HandleAsync(_instances, CancellationToken.None)
                : new Dictionary<InstanceKey, ModpackVersionDto>();
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenFolder(InstalledInstance instancia) => Desktop.OpenFolder(instancia.Path);

    /// <summary>
    ///     Pergunta antes, porque as duas saídas são irreversíveis de maneiras
    ///     diferentes: atualizar troca os mods desta instalação, e criar outra
    ///     ocupa o disco de novo com um mundo em branco. Escolher por ele
    ///     acertaria metade das vezes.
    /// </summary>
    private async Task UpdateAsync(InstalledInstance instancia, ModpackVersionDto novidade)
    {
        if (Shell.Pairing?.Config is not { } config || _busy)
            return;

        var temMundo = Worlds.HasWorld(instancia.Key);

        var escolha = await Dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = $"Atualizar para v{novidade.Version}",
            MarkupMessage = new MarkupString(
                $"<b>{instancia.Manifest.ModpackName}</b> está na v{instancia.Manifest.Version}.<br/><br/>"
                + "<b>Atualizar esta instância</b> troca os mods e mantém o seu mundo, as suas "
                + "configurações e a RAM escolhida."
                + (temMundo
                    ? "<br/><br/>Uma cópia do mundo é guardada antes de mexer em qualquer coisa. "
                      + "Se a cópia falhar, a atualização é cancelada."
                    : "")
                + "<br/><br/><b>Criar nova instância</b> instala a v"
                + $"{novidade.Version} numa pasta à parte, com mundo próprio, e deixa esta como está."),
            YesText = "Atualizar esta",
            NoText = "Criar nova instância",
            CancelText = "Cancelar"
        });

        if (escolha is null)
            return;

        _busy = true;

        try
        {
            var pack = PackDe(instancia);

            var resultado = escolha is true
                ? await Updater.HandleAsync(
                    config.ServerUrl, pack, novidade.Id, instancia,
                    backupWorld: true, Acompanhar(temMundo), CancellationToken.None)
                : await Installer.HandleAsync(
                    config.ServerUrl, pack, novidade.Id, target: null,
                    Acompanhar(false), CancellationToken.None);

            Snackbar.Add(
                resultado.Succeeded
                    ? $"{instancia.Manifest.ModpackName} v{novidade.Version} pronto."
                    : resultado.Error!,
                resultado.Succeeded ? Severity.Success : Severity.Error);
        }
        finally
        {
            _busy = false;
            _busyLabel = null;
        }

        await LoadAsync();
    }

    private Progress<InstallProgress> Acompanhar(bool comBackup) =>
        new Progress<InstallProgress>(p =>
        {
            _busyLabel = p.Phase switch
            {
                InstallPhase.BackingUp => "Copiando o mundo…",
                InstallPhase.Downloading => "Baixando arquivos…",
                InstallPhase.Materializing => "Instalando…",
                InstallPhase.Cleaning => "Limpando o que sobrou…",
                InstallPhase.Done => null,
                _ => comBackup ? "Preparando…" : "Planejando…"
            };

            InvokeAsync(StateHasChanged);
        });

    /// <summary>
    ///     O modpack, reconstruído a partir do manifesto local.
    ///     Evita depender do catálogo para atualizar: tudo o que o instalador
    ///     precisa saber sobre o pack já está gravado na instância, e ir buscá-lo
    ///     de novo seria uma ida à rede a mais num caminho que já tem várias.
    /// </summary>
    private static ModpackDto PackDe(InstalledInstance instancia) => new()
    {
        Id = instancia.Manifest.ModpackId,
        Slug = "",
        Name = instancia.Manifest.ModpackName,
        MinecraftVersion = instancia.Manifest.MinecraftVersion ?? "",
        Loader = instancia.Manifest.Loader ?? ModLoader.Vanilla
    };

    /// <summary>
    ///     Marca como ativa e leva para a tela de jogar.
    ///     Navegar faz parte da ação: o clique é "quero jogar esta", e deixar o
    ///     jogador na lista depois de escolher obrigaria-o a descobrir sozinho
    ///     que o resultado está noutro sítio.
    /// </summary>
    private async Task ActivateAsync(InstalledInstance instancia)
    {
        _busy = true;

        try
        {
            await Active.SetAsync(instancia.Key, CancellationToken.None);
            _active = instancia.Key;
        }
        finally
        {
            _busy = false;
        }

        Navigation.NavigateTo("/");
    }

    /// <summary>
    ///     Confirma antes de apagar, e o texto diz o que se perde.
    ///     Remover leva o mundo do jogador junto — é a única ação do launcher
    ///     que destrói algo que não dá para baixar de novo.
    /// </summary>
    private async Task RemoveAsync(InstalledInstance instancia)
    {
        var confirmado = await Dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = "Remover instância",
            MarkupMessage = new MarkupString(
                $"Isto apaga <b>{instancia.Manifest.ModpackName}</b> e tudo que está na pasta dela, "
                + "<b>inclusive os mundos</b> criados nesta instância.<br/><br/>"
                + "Os mods continuam no store compartilhado e não precisarão ser baixados de novo."),
            YesText = "Remover",
            CancelText = "Cancelar"
        });

        if (confirmado is not true)
            return;

        _busy = true;

        try
        {
            await Instances.RemoveAsync(instancia, CancellationToken.None);

            Snackbar.Add($"{instancia.Manifest.ModpackName} removido.", Severity.Success);

            await LoadAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
