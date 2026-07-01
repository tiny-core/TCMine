namespace TCMine_Domain.Launcher;

/// <summary>Servidor de um modpack (para o servers.dat e o auto-join). Valor puro de domínio.</summary>
public sealed record ModpackServer(string Name, string Address, int Port)
{
    /// <summary>Endereço no formato <c>host:porta</c> (chave do servers.dat).</summary>
    public string Ip => $"{Address}:{Port}";
}
