namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     Identidade de uma instância local do jogo.
///     A chave é (modpack, versão) e não o servidor. Três servidores rodando a
///     mesma versão do mesmo pack compartilha uma instalação só, com três
///     entradas no servers.dat — não faz sentido ocupar o disco três vezes com
///     arquivos idênticos.
///     A consequência: se o admin subir um servidor para a versão 1.6 e deixar
///     outro em 1.5.0, o jogador passa a ter duas instâncias. Está correto, mas
///     vale deixar visível na interface para ninguém se assustar com o disco.
/// </summary>
public readonly record struct InstanceKey(Guid ModpackId, Guid ModpackVersionId)
{
    /// <summary>
    ///     Nome curto e estável para a pasta.
    ///     Nada de usar o nome do modpack aqui: acento, barra e dois-pontos
    ///     quebram em algum sistema de arquivos, e renomear o pack renomearia a
    ///     pasta, forçando download completo de novo.
    /// </summary>
    public string ToDirectoryName() => $"{ModpackId:N}{ModpackVersionId:N}"[..24];
}
