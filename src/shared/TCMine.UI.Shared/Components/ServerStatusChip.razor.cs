using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Servers;

namespace TCMine.UI.Shared.Components;

public partial class ServerStatusChip : ComponentBase
{
    [Parameter] [EditorRequired] public GameServerStatus Status { get; set; }

    // Rótulos em português num lugar só. Espalhar switch de tradução pelas
    // telas é o caminho mais curto para "Running" aparecer em inglês numa
    // página e traduzido em outra.
    // Minúsculas: regra do TCMine Design System para chip ("chips em
    // minúsculas", docs/ROADMAP.md) — a mesma convenção que a Visão geral e
    // a página pública já seguem com os próprios switches de tradução.
    private string Label => Status switch
    {
        GameServerStatus.Stopped => "parado",
        GameServerStatus.Starting => "iniciando",
        GameServerStatus.Running => "online",
        GameServerStatus.Stopping => "parando",
        GameServerStatus.Crashed => "falhou",
        GameServerStatus.Updating => "atualizando",
        _ => "desconhecido"
    };
}
