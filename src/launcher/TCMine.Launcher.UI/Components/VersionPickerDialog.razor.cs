using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     Pergunta que versão instalar, e devolve o id escolhido.
///     Carrega a lista ela própria em vez de a receber pronta: quem abre o
///     diálogo quer perguntar, não quer saber do canal — e as duas telas que o
///     usam teriam de repetir a mesma consulta e o mesmo tratamento de falha.
/// </summary>
public partial class VersionPickerDialog : ComponentBase
{
    private string? _error;
    private bool _loading = true;
    private Guid _selected;
    private IReadOnlyList<ModpackVersionSummaryDto> _versions = [];

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    /// <summary>A versão já instalada, para a marcar. Vazio quando é instalação nova.</summary>
    [Parameter] public Guid Current { get; set; }

    [Parameter] public string ConfirmLabel { get; set; } = "Instalar";

    [Inject] private IServerConnection Connection { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _versions = await Connection.GetVersionsAsync(ModpackId, CancellationToken.None);

            // Pré-seleciona a mais recente: é a escolha certa na maioria das
            // vezes, e quem abriu o seletor para pegar outra só precisa de a
            // clicar. Abrir sem nada selecionado tornaria o botão inerte até um
            // clique que não acrescenta informação nenhuma.
            _selected = _versions.Count > 0 ? _versions[0].Id : Guid.Empty;
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

    private void Confirmar() => Dialog.Close(DialogResult.Ok(_selected));

    private void Cancelar() => Dialog.Cancel();
}
