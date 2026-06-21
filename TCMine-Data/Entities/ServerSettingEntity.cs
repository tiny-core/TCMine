using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

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
    
    /// <summary>
    /// Token da API do CurseForge — CIFRADO em repouso (nunca texto puro no banco)
    /// </summary> 
    [MaxLength(256)]
    public string? CfApiKeyEncrypted { get; set; }

    /// <summary>
    /// Application (client) ID do app registrado no Azure AD — identificador público
    /// </summary>
    [MaxLength(64)]
    public string? AzureClientId { get; set; }

    /// <summary>
    /// Directory (tenant) ID do Azure AD; "consumers"/"common" para contas pessoais.
    /// Identificador público (como o client id) — NÃO é segredo, fica em texto.
    /// </summary>
    [MaxLength(64)]
    public string? AzureTenantId { get; set; }

    /// <summary>
    /// URL base pública do servidor (ex.: https://tcmine.net). O launcher é compilado pelo
    /// servidor e aponta para "{PublicBaseUrl}/updates" como feed de update do Velopack.
    /// Identificador público — texto. (Consumidor: build do launcher, mais adiante.)
    /// </summary>
    [MaxLength(256)]
    public string? PublicBaseUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}