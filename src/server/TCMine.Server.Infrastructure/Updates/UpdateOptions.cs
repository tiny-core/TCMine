namespace TCMine.Server.Infrastructure.Updates;

/// <summary>Seção "Updates" da configuração.</summary>
public sealed class UpdateOptions
{
    /// <summary>
    ///     Desligável para instalação sem saída para a internet, ou para quem
    ///     não quer o painel consultando o GitHub.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Repositório (dono/nome) cujas releases "server-v*" são o canal de atualização.</summary>
    public string Repository { get; set; } = "tiny-core/TCMine";
}
