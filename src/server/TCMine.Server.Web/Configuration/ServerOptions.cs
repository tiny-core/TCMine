namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Configuração de identidade e distribuição desta instalação.
///     É o que o admin preenche ao hospedar o TCMine, e é daqui que sai o
///     tcmine.json carimbado no download do launcher.
/// </summary>
public sealed class ServerOptions
{
    public const string SectionName = "Server";

    /// <summary>Nome exibido no launcher dos jogadores.</summary>
    public string Name { get; set; } = "TCMine Server";

    /// <summary>
    ///     Endereço público desta instalação.
    ///     Precisa ser o endereço que o jogador alcança de fora, não o IP
    ///     interno do container: é ele que vai no tcmine.json.
    /// </summary>
    public Uri? PublicUrl { get; set; }

    /// <summary>
    ///     Endereço desta instalação visto de DENTRO dos containers de jogo, para a
    ///     nuvem de itens (mod tccloud). Opcional: sem ele vale o PublicUrl. Útil
    ///     quando o endereço público não é alcançável de dentro do Docker (NAT
    ///     sem hairpin) — ex.: <c>http://tcmine:8080</c> numa rede Docker comum.
    /// </summary>
    public Uri? CloudUrl { get; set; }

    /// <summary>
    ///     Client ID da app Azure usada no login com a Microsoft. Público por
    ///     natureza — o fluxo do Minecraft usa public client com PKCE.
    ///     SEMENTE, não fonte da verdade: quem manda é o valor gravado pela tela
    ///     de configurações, e esta chave só é consultada enquanto lá estiver
    ///     vazio. Existe para que instalação configurada por arquivo ou por
    ///     variável de ambiente continue funcionando sem migração manual.
    /// </summary>
    public string AzureClientId { get; set; } = string.Empty;

    /// <summary>
    ///     Congela os clientes na versão atual do launcher. Útil durante um
    ///     evento, para ninguém atualizar no meio da partida.
    /// </summary>
    public bool FreezeLauncherUpdates { get; set; }

    /// <summary>
    ///     Força atualização mesmo dentro do mesmo protocolo. Freio de
    ///     emergência para bug crítico ou falha de segurança.
    /// </summary>
    public string? MinLauncherVersion { get; set; }
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"Postgres" ou "Sqlite". Postgres em produção.</summary>
    public string Provider { get; set; } = "Postgres";

    public string ConnectionString { get; set; } = string.Empty;
}
