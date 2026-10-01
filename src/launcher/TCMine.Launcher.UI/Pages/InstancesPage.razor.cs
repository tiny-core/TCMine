using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Runtime;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.UI.Abstractions;
using TCMine.Launcher.UI.State;
using TCMine.UI.Shared.Formatting;

namespace TCMine.Launcher.UI.Pages;

public partial class InstancesPage : ComponentBase, IDisposable
{
    private InstanceKey? _active;
    private bool _busy;

    private IReadOnlyDictionary<InstanceKey, ModpackVersionDto> _updates =
        new Dictionary<InstanceKey, ModpackVersionDto>();

    /// <summary>Bytes em JREs que nenhuma instância pede. Zero esconde o botão.</summary>
    private long _reclaimable;
    private IReadOnlyList<InstalledInstance> _instances = [];
    private bool _loading = true;

    [Inject] private ListInstances Instances { get; set; } = default!;

    [Inject] private ChooseInstance Active { get; set; } = default!;

    [Inject] private CheckInstanceUpdates Updates { get; set; } = default!;

    [Inject] private UpdateInstance Updater { get; set; } = default!;

    [Inject] private InstallModpackVersion Installer { get; set; } = default!;

    [Inject] private IWorldBackup Worlds { get; set; } = default!;

    [Inject] private CleanupJavaRuntimes JavaCleanup { get; set; } = default!;

    [Inject] private SetInstanceMemory Memory { get; set; } = default!;

    [Inject] private LauncherShellState Shell { get; set; } = default!;

    [Inject] private InstallOperationState Operation { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IDesktopShell Desktop { get; set; } = default!;

    [Inject] private IDialogService Dialogs { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    public void Dispose()
    {
        Operation.Changed -= OnOperationChanged;
        GC.SuppressFinalize(this);
    }

    protected override Task OnInitializedAsync()
    {
        Operation.Changed += OnOperationChanged;
        return LoadAsync();
    }

    private void OnOperationChanged() => InvokeAsync(StateHasChanged);

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _instances = await Instances.HandleAsync(CancellationToken.None);

            // Passa _instances adiante em vez de deixar cada caso de uso listar
            // a pasta de instâncias de novo por dentro: eram três varreduras do
            // disco (cada uma somando o tamanho de mundos inteiros) para uma
            // única abertura de tela.

            // A ativa vem resolvida, e não lida em bruto: com uma instância só
            // ela é a ativa sem ninguém ter escolhido, e a etiqueta tem de dizer
            // o mesmo que a tela de jogar mostra.
            _active = (await Active.CurrentAsync(_instances, CancellationToken.None)).Active?.Key;

            // Só com servidor: sem ele não há como saber, e "não saber" não é o
            // mesmo que "não há novidade" — mostrar botões que falham ao clicar
            // seria pior do que não mostrar nenhum.
            _updates = Shell.IsOnline
                ? await Updates.HandleAsync(_instances, CancellationToken.None)
                : new Dictionary<InstanceKey, ModpackVersionDto>();

            _reclaimable = (await JavaCleanup.FindUnusedAsync(_instances, CancellationToken.None))
                .Sum(r => r.SizeBytes);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenFolder(InstalledInstance instance) => Desktop.OpenFolder(instance.Path);

    /// <summary>
    ///     Grava a RAM da instância.
    ///     Vazio volta à recomendada do pack, e isso é escolha e não engano: o
    ///     jogador que apaga o número está a dizer "decide tu".
    /// </summary>
    private async Task SetMemoryAsync(InstalledInstance instance, int? megabytes)
    {
        var result = await Memory.HandleAsync(instance, megabytes, CancellationToken.None);

        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Warning);
            return;
        }

        // Relê em vez de mexer no objeto em memória: o manifesto no disco é a
        // verdade, e uma cópia editada à mão aqui seria uma segunda versão dela.
        await LoadAsync();
    }

    /// <summary>
    ///     Apaga os JREs que sobraram.
    ///     Sem confirmação de propósito: é cache, o pior caso é voltar a
    ///     descarregá-lo, e uma pergunta aqui seria cerimónia sobre algo que o
    ///     jogador pediu explicitamente ao ver o tamanho no botão.
    /// </summary>
    private async Task CleanupJavaAsync()
    {
        _busy = true;

        try
        {
            var freed = await JavaCleanup.HandleAsync(_instances, CancellationToken.None);

            Snackbar.Add(
                freed > 0
                    ? $"{HumanSize.Bytes(freed)} libertados."
                    : "Nada a libertar agora — feche o jogo e tente de novo.",
                freed > 0 ? Severity.Success : Severity.Info);
        }
        finally
        {
            _busy = false;
        }

        await LoadAsync();
    }

    /// <summary>
    ///     Pergunta antes, porque as duas saídas são irreversíveis de maneiras
    ///     diferentes: atualizar troca os mods desta instalação, e criar outra
    ///     ocupa o disco de novo com um mundo em branco. Escolher por ele
    ///     acertaria metade das vezes.
    ///     Escolher a VERSÃO não é oferecido aqui, de propósito. Uma instância
    ///     existente só anda para a frente: descer para uma versão antiga por
    ///     cima dela parte os mundos já jogados, porque os mods que sumissem
    ///     levariam consigo os blocos e itens que registaram. Quem quer uma
    ///     versão antiga instala-a pelo catálogo, e ela nasce ao lado.
    /// </summary>
    private async Task UpdateAsync(InstalledInstance instance, ModpackVersionDto newer)
    {
        if (Shell.Pairing?.Config is not { } config || _busy || Operation.IsRunning)
            return;

        var temMundo = Worlds.HasWorld(instance.Key);

        // Uma instância alpha não se duplica. O canal alpha existe para ACOMPANHAR
        // pré-lançamentos, e cada cópia que ficasse para trás seria uma instalação
        // presa numa alpha que ninguém mais vai atualizar — lixo no disco com cara
        // de instância válida. Quem quer uma segunda instalação escolhe o canal no
        // catálogo, onde a decisão é consciente.
        var ehAlpha = ReleaseChannels.Of(instance.Manifest.Version) is ReleaseChannel.Alpha;

        var choice = await Dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = $"Atualizar para v{newer.Version}",
            MarkupMessage = new MarkupString(
                $"<b>{instance.Manifest.ModpackName}</b> está na v{instance.Manifest.Version}."
                + (ehAlpha ? " Esta instância acompanha o canal <b>alpha</b>." : "")
                + "<br/><br/><b>Atualizar esta instância</b> troca os mods e mantém o seu mundo, as "
                + "suas configurações e a RAM escolhida."
                + (temMundo
                    ? "<br/><br/>Uma cópia do mundo é guardada antes de mexer em qualquer coisa. "
                      + "Se a cópia falhar, a atualização é cancelada."
                    : "")
                + (ehAlpha
                    ? ""
                    : "<br/><br/><b>Criar nova instância</b> instala a v"
                      + $"{newer.Version} numa pasta à parte, com mundo próprio, e deixa esta "
                      + "como está.")),
            YesText = "Atualizar esta",
            NoText = ehAlpha ? null : "Criar nova instância",
            CancelText = "Cancelar"
        });

        if (choice is null)
            return;

        _busy = true;
        Operation.Begin(null);

        try
        {
            var pack = PackDe(instance);
            var progress = new Progress<InstallProgress>(Operation.Report);

            var result = choice is true
                ? await Updater.HandleAsync(
                    config.ServerUrl, pack, newer.Id, instance,
                    backupWorld: true, progress, CancellationToken.None)
                : await Installer.HandleAsync(
                    config.ServerUrl, pack, newer.Id, target: null,
                    progress, CancellationToken.None);

            Snackbar.Add(
                result.Succeeded
                    ? $"{instance.Manifest.ModpackName} v{newer.Version} pronto."
                    : result.Error!,
                result.Succeeded ? Severity.Success : Severity.Error);
        }
        finally
        {
            _busy = false;
            Operation.Finish();
        }

        await LoadAsync();
    }

    /// <summary>O texto para a fase atual — null em Done, que já vira o toast de sucesso.</summary>
    private static string? BusyLabel(InstallProgress? progress) => progress?.Phase switch
    {
        InstallPhase.BackingUp => "Copiando o mundo…",
        InstallPhase.Downloading => "Baixando arquivos…",
        InstallPhase.Materializing => "Instalando…",
        InstallPhase.Cleaning => "Limpando o que sobrou…",
        InstallPhase.Done => null,
        _ => "Preparando…"
    };

    /// <summary>
    ///     O modpack, reconstruído a partir do manifesto local.
    ///     Evita depender do catálogo para atualizar: tudo o que o instalador
    ///     precisa saber sobre o pack já está gravado na instância, e ir buscá-lo
    ///     de novo seria uma ida à rede a mais num caminho que já tem várias.
    /// </summary>
    private static ModpackDto PackDe(InstalledInstance instance) => new()
    {
        Id = instance.Manifest.ModpackId,
        Slug = "",
        Name = instance.Manifest.ModpackName,
        MinecraftVersion = instance.Manifest.MinecraftVersion ?? "",
        Loader = instance.Manifest.Loader ?? ModLoader.Vanilla
    };

    /// <summary>
    ///     Marca como ativa e leva para a tela de jogar.
    ///     Navegar faz parte da ação: o clique é "quero jogar esta", e deixar o
    ///     jogador na lista depois de escolher obrigaria-o a descobrir sozinho
    ///     que o resultado está noutro sítio.
    /// </summary>
    private async Task ActivateAsync(InstalledInstance instance)
    {
        _busy = true;

        try
        {
            await Active.SetAsync(instance.Key, CancellationToken.None);
            _active = instance.Key;
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
    private async Task RemoveAsync(InstalledInstance instance)
    {
        var confirmado = await Dialogs.ShowMessageBoxAsync(new MessageBoxOptions
        {
            Title = "Remover instância",
            MarkupMessage = new MarkupString(
                $"Isto apaga <b>{instance.Manifest.ModpackName}</b> e tudo que está na pasta dela, "
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
            await Instances.RemoveAsync(instance, CancellationToken.None);

            Snackbar.Add($"{instance.Manifest.ModpackName} removido.", Severity.Success);

            await LoadAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
