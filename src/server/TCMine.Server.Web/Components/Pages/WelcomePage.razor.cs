using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Settings;
using TCMine.Server.Web.Components.Features.Network;

namespace TCMine.Server.Web.Components.Pages;

/// <summary>
///     O assistente de primeira configuração: entrar → rede → rede de casa e
///     portas. Abre sozinho (pela visão geral) enquanto o modo de rede nunca foi
///     escolhido — instalação nova, ou uma de antes da escolha única, que o vê
///     uma vez com o modo que já vale marcado.
///     Edita o mesmo <see cref="NetworkForm" /> da aba Rede, com os mesmos
///     componentes: duas telas para a mesma configuração só não divergem se
///     forem a mesma.
/// </summary>
public partial class WelcomePage : ComponentBase
{
    private static readonly string[] Steps = ["Entrar", "Como os jogadores chegam", "Rede de casa e portas", "Concluir"];

    private NetworkForm? _form;

    /// <summary>O passo mais adiante já visitado: as fichas só levam até ele.</summary>
    private int _furthest;

    private bool _saving;
    private InstallationSettings? _settings;
    private int _step;

    [Inject] private ICurrentUserScope Scope { get; set; } = default!;
    [Inject] private ISettingsRepository Repository { get; set; } = default!;
    [Inject] private UpdateSettings UpdateUseCase { get; set; } = default!;
    [Inject] private GetAddressSettings AddressLookup { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // A configuração é da instalação: quem não é admin dela não tem o que
        // fazer aqui, e a visão geral nunca o manda para cá.
        if (!Scope.IsInstanceAdmin)
        {
            Navigation.NavigateTo("/admin");
            return;
        }

        _settings = await Repository.GetAsync(CancellationToken.None);
        _form = NetworkForm.From(_settings);
    }

    /// <summary>Como na aba Rede: a detecção sai para a rede, então só depois de a tela estar desenhada.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _form is null)
            return;

        await _form.DetectAsync(AddressLookup);
        StateHasChanged();
    }

    private string StepIcon(int index) =>
        index < _step || index == 0 ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.RadioButtonUnchecked;

    private void GoTo(int step)
    {
        _step = Math.Clamp(step, 0, Steps.Length - 1);
        _furthest = Math.Max(_furthest, _step);
    }

    /// <summary>Grava a rede como ficou no assistente, por cima do resto da configuração como está.</summary>
    private Task FinishAsync() =>
        SaveAsync(_form!.ApplyTo(UpdateSettingsCommand.From(_settings!)), "Pronto. Agora é criar um modpack.");

    /// <summary>
    ///     Marca o modo que já vale como escolhido, sem gravar o que foi digitado.
    ///     Sem gravar nada, o assistente voltaria a cada login.
    /// </summary>
    private Task SkipAsync() =>
        SaveAsync(
            UpdateSettingsCommand.From(_settings!) with { NetworkMode = _settings!.EffectiveNetworkMode },
            "Tudo bem. A rede fica em Configurações → Rede quando quiser.");

    private async Task SaveAsync(UpdateSettingsCommand command, string message)
    {
        _saving = true;
        try
        {
            var result = await UpdateUseCase.HandleAsync(command, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            Snackbar.Add(message, Severity.Success);
            Navigation.NavigateTo("/admin");
        }
        finally
        {
            _saving = false;
        }
    }
}
