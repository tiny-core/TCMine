namespace TCMine.Server.Data.Entities;

/// <summary>
/// Configuração da aplicação persistida no banco — linha única (Id == 1).
///
/// Diferente da config de bootstrap (provider/connection string, que precisa ficar
/// fora do banco), estes valores são editáveis em runtime pelo painel admin:
/// token do CurseForge e identificadores do Azure (login Microsoft).
///
/// Segredos (CF token) são guardados <b>cifrados</b> via Data Protection — ver
/// <see cref="Services.ServerSettingsService"/>. O <see cref="AzureClientId"/> e o
/// <see cref="AzureTenantId"/> são identificadores públicos, guardados em texto.
/// </summary>
public class ServerSettingEntity
{
    // Chave fixa da linha única de settings
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    // Token da API do CurseForge — CIFRADO em repouso (nunca texto puro no banco)
    public string? CfApiKeyEncrypted { get; set; }

    // Application (client) ID do app registrado no Azure AD — identificador público
    public string? AzureClientId { get; set; }

    // Directory (tenant) ID do Azure AD; "consumers"/"common" para contas pessoais.
    // Identificador público (como o client id) — NÃO é segredo, fica em texto.
    public string? AzureTenantId { get; set; }

    // URL base pública do servidor (ex.: https://tcmine.net). O launcher é compilado pelo
    // servidor e aponta para "{PublicBaseUrl}/updates" como feed de update do Velopack.
    // Identificador público — texto. (Consumidor: build do launcher, mais adiante.)
    public string? PublicBaseUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}