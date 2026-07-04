using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Infrastructure.Server;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin;

/// <summary>
/// Página de configurações do servidor (token CurseForge + Azure client/tenant id).
/// Segue a política de escrita-só-ao-Guardar: o formulário vive em memória e nada é
/// persistido até o clique em "Guardar".
///
/// Nota: por enquanto qualquer admin autenticado acede (a guarda é o &lt;AuthorizeView&gt;
/// do AdminLayout). Na Etapa C, com usuários e papéis, isto fica restrito ao Owner.
/// </summary>
public partial class Settings : ComponentBase
{
    [Inject] private ServerSettingsService SettingsService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // Modelo isolado do formulário — nunca usar a própria Page como modelo
    private readonly SettingsForm _form = new();

    private bool _loading = true;
    private bool _saving;
    private bool _showCfKey; // revela/oculta o token na UI

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando configurações…", async () =>
        {
            // Carrega os valores guardados (descriptografados) para edição
            var stored = await SettingsService.GetStoredAsync();
            _form.CfApiKey = stored.CfApiKey ?? string.Empty;
            _form.AzureClientId = stored.AzureClientId ?? string.Empty;
            _form.AzureTenantId = stored.AzureTenantId ?? string.Empty;
            _form.PublicBaseUrl = stored.PublicBaseUrl ?? string.Empty;
            _loading = false;
        });
    }

    private async Task SaveAsync()
    {
        // A URL pública é embutida no launcher como `new Uri(...)`; sem esquema ela quebra o launcher no
        // boot (UriFormatException). Valida cedo, aqui, para o admin não gerar um launcher inutilizável.
        var url = _form.PublicBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(url) &&
            (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
             (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
        {
            Snackbar.Add("A URL pública deve ser absoluta e começar com http:// ou https://.", Severity.Warning);
            return;
        }

        _saving = true;
        try
        {
            await Busy.RunAsync("Salvando configurações…", () => SettingsService.SaveAsync(new ServerSettings(
                _form.CfApiKey, _form.AzureClientId, _form.AzureTenantId, _form.PublicBaseUrl)));
            Snackbar.Add("Configurações salvas.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao salvar: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    // Modelo do formulário (mutável; o ServerSettingsService faz o trim/normalização)
    private sealed class SettingsForm
    {
        public string CfApiKey { get; set; } = string.Empty;
        public string AzureClientId { get; set; } = string.Empty;
        public string AzureTenantId { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}