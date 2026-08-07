using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Settings;

/// <summary>
///     Configuração operacional da instalação, editável pelo painel.
///     Linha única: é a configuração DESTE TCMine, não uma coleção.
///     O que muda no deploy (conexão do banco, endpoint do Docker, caminho dos
///     blobs) continua em appsettings — mexer nisso exige reiniciar de qualquer
///     forma, e não deve depender do banco estar de pé.
/// </summary>
public sealed class InstallationSettings : Entity
{
    // ---------- Padrões para novos modpacks ----------

    /// <summary>Versão do Minecraft já selecionada ao criar um modpack. Nula = sem padrão.</summary>
    public string? DefaultMinecraftVersion { get; set; }

    public ModLoader DefaultLoader { get; set; } = ModLoader.NeoForge;

    /// <summary>RAM sugerida (MB) para novas versões.</summary>
    public int DefaultMemoryMb { get; set; } = 4096;

    // ---------- Integrações ----------

    /// <summary>
    ///     Chave da API do CurseForge, cifrada em repouso. Sem ela, o resolver do
    ///     CurseForge se declara indisponível e o sistema segue só com Modrinth.
    ///     Nunca é devolvida à UI — só se informa se existe ou não.
    /// </summary>
    public string? CurseForgeApiKeyEncrypted { get; set; }

    // ---------- E-mail (recuperação de senha, convites) ----------

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }

    /// <summary>Senha do SMTP, cifrada em repouso. Mesma regra da chave do CurseForge.</summary>
    public string? SmtpPasswordEncrypted { get; set; }

    /// <summary>Remetente das mensagens, ex.: "TCMine &lt;nao-responda@exemplo.com&gt;".</summary>
    public string? SmtpFrom { get; set; }

    public bool SmtpUseTls { get; set; } = true;

    /// <summary>Há SMTP suficiente para tentar enviar?</summary>
    public bool HasSmtp => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(SmtpFrom);
}
