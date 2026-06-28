using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Servers.Dialogs;

/// <summary>
/// Diálogo de criar/editar uma instância de servidor. Só coleta e valida o formulário e devolve um
/// <see cref="ServerInstanceEditDto"/>; a persistência fica com o <see cref="ServerInstanceService"/>,
/// chamado pela página. Carrega as opções de modpack ao abrir (consulta leve, sem overlay — exceção
/// do padrão para buscas internas de diálogo).
/// </summary>
public partial class ServerInstanceEditDialog : ComponentBase
{
    [Inject] private ServerInstanceService Service { get; set; } = null!;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    // null = criar; preenchido = editar (não mutamos o original — devolvemos um DTO)
    [Parameter] public ServerInstanceEditDto? Instance { get; set; }

    // Na criação a partir do hub de um modpack: pré-seleciona e trava o modpack de origem
    [Parameter] public Guid? PresetModpackId { get; set; }

    // Modpack travado: edição (não muda após criar) ou criação já amarrada a um modpack
    private bool LockModpack => IsEdit || PresetModpackId is not null;

    private MudForm _form = null!;
    private List<ModpackOptionDto> _modpacks = [];

    private string _name = string.Empty;
    private Guid _modpackId;
    private int _port = 25565;
    private int _maxPlayers = 20;
    private int _ramMb = 4096;
    private int _xmsMb;
    private string _motd = "A TCMine server";
    private string _extraJvmArgs = string.Empty;
    private bool _autoRestart;
    private string _publicAddress = string.Empty;
    private bool _advertise = true;

    private bool IsEdit => Instance is not null;

    protected override async Task OnInitializedAsync()
    {
        _modpacks = await Service.ListModpackOptionsAsync();

        if (Instance is { } i)
        {
            _name = i.Name;
            _modpackId = i.ModpackId;
            _port = i.Port;
            _maxPlayers = i.MaxPlayers;
            _ramMb = i.RamMb;
            _xmsMb = i.XmsMb;
            _motd = i.Motd;
            _extraJvmArgs = i.ExtraJvmArgs;
            _autoRestart = i.AutoRestart;
            _publicAddress = i.PublicAddress;
            _advertise = i.Advertise;
        }
        else
        {
            // Criação: pré-preenche o endereço público com o IP do host (os jogadores na rede usam esse)
            _publicAddress = ServerInstanceService.GetLocalHostAddress();

            if (PresetModpackId is { } preset)
                _modpackId = preset; // a partir do hub: já amarrada ao modpack de origem
            else if (_modpacks.Count > 0)
                _modpackId = _modpacks[0].Id; // primeiro modpack (campo obrigatório)
        }
    }

    private async Task Confirm()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid || _modpackId == Guid.Empty) return;

        var dto = new ServerInstanceEditDto(
            Instance?.Id ?? Guid.Empty, _name.Trim(), _modpackId, _port, _ramMb, _xmsMb,
            _maxPlayers, _motd.Trim(), _extraJvmArgs.Trim(), _autoRestart, _publicAddress.Trim(), _advertise);

        MudDialog.Close(DialogResult.Ok(dto));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
