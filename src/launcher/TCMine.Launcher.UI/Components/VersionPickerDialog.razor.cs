using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Pergunta que versão instalar, e devolve o id escolhido.
///     Serve a INSTALAÇÃO e só ela. Escolher versão numa atualização seria poder
///     descer para uma mais antiga por cima de uma instância existente, e isso
///     parte mundos já jogados: os mods que sumissem levariam consigo os blocos e
///     itens que eles registaram. Quem quer uma versão antiga instala-a ao lado.
///     Carrega a lista ele próprio em vez de a receber pronta: quem o abre quer
///     perguntar, não quer saber do canal.
/// </summary>
public partial class VersionPickerDialog : ComponentBase
{
    private ReleaseChannel _channel = ReleaseChannel.Release;
    private string? _error;
    private bool _loading = true;
    private Guid _selected;
    private IReadOnlyList<ModpackVersionSummaryDto> _versions = [];

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    /// <summary>
    ///     As versões que já estão no disco. Aparecem marcadas e desligadas:
    ///     instalar a mesma outra vez daria duas instâncias idênticas, que
    ///     ocupam o dobro do disco e ficam indistinguíveis na lista.
    /// </summary>
    [Parameter] public IReadOnlySet<Guid> InstalledVersionIds { get; set; } =
        new HashSet<Guid>();

    [Inject] private IServerConnection Connection { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    /// <summary>
    ///     Troca de canal e recarrega.
    ///     A lista vem do servidor por canal e não é filtrada aqui: o que cada
    ///     canal oferece é decisão dele, e reparti-la na tela seria uma segunda
    ///     cópia da regra, pronta a discordar.
    /// </summary>
    private async Task ChangeChannelAsync(ReleaseChannel channel)
    {
        if (_channel == channel)
            return;

        _channel = channel;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            _versions = await Connection.GetVersionsAsync(ModpackId, _channel, CancellationToken.None);

            // Pré-seleciona a mais recente QUE AINDA NÃO ESTÁ INSTALADA: é a
            // escolha certa na maioria das vezes, e cair numa já instalada
            // abriria o diálogo com o botão desligado, parecendo avariado.
            _selected = _versions.FirstOrDefault(v => !IsInstalled(v.Id))?.Id ?? Guid.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _error = "Não foi possível obter a lista de versões. " + ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private bool IsInstalled(Guid versionId) => InstalledVersionIds.Contains(versionId);

    private void Confirmar() => Dialog.Close(DialogResult.Ok(_selected));

    private void Cancelar() => Dialog.Cancel();
}
