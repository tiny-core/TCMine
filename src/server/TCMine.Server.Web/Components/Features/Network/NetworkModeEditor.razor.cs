using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Server.Application.Dns;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Features.Network;

/// <summary>
///     Os cartões do modo de rede e o que vem com cada um (o domínio, a
///     Cloudflare e a sincronização dela). Edita o <see cref="Form" /> que
///     recebe; quem grava é a página que o hospeda.
/// </summary>
public partial class NetworkModeEditor : ComponentBase
{
    /// <summary>
    ///     A última sincronização — a automática, ou a pedida nesta tela. Nulo =
    ///     nenhuma desde que o painel subiu.
    /// </summary>
    private DnsSyncOutcome? _dnsOutcome;

    private bool _isSyncingDns;

    [Parameter] [EditorRequired] public NetworkForm Form { get; set; } = default!;

    /// <summary>A página está gravando: o "Sincronizar agora" espera.</summary>
    [Parameter] public bool Busy { get; set; }

    [Inject] private SyncGameDns DnsSync { get; set; } = default!;
    [Inject] private DnsSyncState DnsState { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private DnsSyncReport? _dnsReport => _dnsOutcome?.Report;

    /// <summary>"agora mesmo", "há 4 min": o bastante para saber se o resultado é fresco.</summary>
    private string DnsOutcomeAge
    {
        get
        {
            if (_dnsOutcome is null)
                return "";

            var minutes = (int)(DateTimeOffset.UtcNow - _dnsOutcome.At).TotalMinutes;

            return minutes switch
            {
                < 1 => "agora mesmo",
                < 60 => $"há {minutes} min",
                _ => $"há {minutes / 60} h"
            };
        }
    }

    protected override void OnInitialized() => _dnsOutcome = DnsState.Last;

    private static string CardStyle(bool selected) =>
        selected
            ? "cursor:pointer; height:100%; border:2px solid var(--mud-palette-primary);"
            : "cursor:pointer; height:100%; border:2px solid var(--mud-palette-lines-default);";

    /// <summary>
    ///     Sincroniza com o que está GRAVADO, não com o que está digitado: o caso
    ///     de uso lê a configuração do banco, como fará quando rodar sozinho. Por
    ///     isso a tela diz que o botão usa o que está salvo.
    /// </summary>
    private async Task SyncDnsAsync()
    {
        _isSyncingDns = true;
        try
        {
            // Fica registrado no mesmo lugar que a automática: a tela mostra
            // sempre a mais recente, venha de onde vier.
            DnsState.Record(await DnsSync.HandleAsync(CancellationToken.None));
            _dnsOutcome = DnsState.Last;
        }
        finally
        {
            _isSyncingDns = false;
        }
    }

    private async Task CopyDetectedAddress()
    {
        if (Form.DetectedAddress is null)
            return;

        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Form.DetectedAddress);
        Snackbar.Add("IP copiado.", Severity.Info);
    }
}
