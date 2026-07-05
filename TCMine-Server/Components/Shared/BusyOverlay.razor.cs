using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Shared;

/// <summary>
///     Overlay bloqueante global de feedback async. Renderizado uma única vez no <c>RootLayout</c>,
///     reage ao <see cref="BusyService" /> do circuito: aparece enquanto há operação em andamento e some
///     quando a última termina. Não-fechável de propósito (ver markup no .razor).
/// </summary>
public partial class BusyOverlay : ComponentBase, IDisposable
{
    // Container rolável do log de passos — alvo do auto-scroll para manter o passo atual visível
    private ElementReference _stepsEl;

    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>
    ///     Status "global" mostrado sob o spinner: a fase atual sem o detalhe técnico. Por convenção,
    ///     o detalhe ao vivo vem após " — " (ex.: "Instalando NeoForge X — Extracted: …/fastutil.jar");
    ///     aqui ficamos só com o rótulo ("Instalando NeoForge X"). O detalhe completo aparece na lista.
    /// </summary>
    private string GlobalStatus
    {
        get
        {
            var message = Busy.Message ?? "Processando…";
            var i = message.IndexOf(" — ", StringComparison.Ordinal);
            return i < 0 ? message : message[..i];
        }
    }

    public void Dispose()
    {
        Busy.OnChange -= OnBusyChanged;
    }

    protected override void OnInitialized()
    {
        Busy.OnChange += OnBusyChanged;
    }

    // Mantém o passo atual (última linha) visível conforme a lista cresce e passa a rolar
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Busy.IsBusy || Busy.Steps.Count <= 1) return;
        try
        {
            await JS.InvokeVoidAsync("tcmineScrollToBottom", _stepsEl);
        }
        catch
        {
            /* elemento ainda não no DOM / circuito a fechar — ignora */
        }
    }

    // O estado muda fora do ciclo de render do componente; reagenda na sync context do Blazor
    private void OnBusyChanged()
    {
        InvokeAsync(StateHasChanged);
    }
}