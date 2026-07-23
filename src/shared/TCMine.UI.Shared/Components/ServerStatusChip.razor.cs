using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;

namespace TCMine.UI.Shared.Components;

public partial class ServerStatusChip : ComponentBase
{
    [Parameter] [EditorRequired] public GameServerStatus Status { get; set; }

    // Rótulos em português num lugar só. Espalhar switch de tradução pelas
    // telas é o caminho mais curto para "Running" aparecer em inglês numa
    // página e traduzido em outra.
    private string Label => Status switch
    {
        GameServerStatus.Stopped => "Parado",
        GameServerStatus.Starting => "Iniciando",
        GameServerStatus.Running => "Online",
        GameServerStatus.Stopping => "Parando",
        GameServerStatus.Crashed => "Falhou",
        GameServerStatus.Updating => "Atualizando",
        _ => "Desconhecido"
    };
}