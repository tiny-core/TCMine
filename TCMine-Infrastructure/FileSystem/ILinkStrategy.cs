namespace TCMine_Infrastructure.FileSystem;

/// <summary>
/// Estratégia de "ligar" um arquivo/pasta do cache compartilhado para dentro do diretório de uma
/// instância de servidor, <b>sem duplicar bytes quando possível</b>. É o ponto único que isola a
/// diferença entre ambientes:
///
/// <list type="bullet">
/// <item><b>Produção (Linux/Docker)</b> → symlinks reais (arquivo e pasta): instantâneo, custo zero
/// de disco. 500+ mods viram 500 ponteiros.</item>
/// <item><b>Dev (Windows)</b> → symlink no Windows exige admin/Developer Mode; então caímos para
/// hardlink de arquivo (funciona sem privilégio, mesma partição) e cópia recursiva de pasta.</item>
/// </list>
///
/// A escolha é feita uma vez na composição do DI (ver <see cref="LinkStrategyFactory"/>), e o resto
/// do código (provisioner) só conhece esta interface.
/// </summary>
public interface ILinkStrategy
{
    /// <summary>Nome legível da estratégia ativa (para logs/diagnóstico no painel).</summary>
    string Name { get; }

    /// <summary>
    /// Liga <paramref name="source"/> (arquivo existente no cache) em <paramref name="destination"/>.
    /// Cria os diretórios pais do destino. Sobrescreve um destino já existente.
    /// </summary>
    void LinkFile(string source, string destination);

    /// <summary>
    /// Liga <paramref name="source"/> (pasta existente no cache) em <paramref name="destination"/>.
    /// Sobrescreve um destino já existente.
    /// </summary>
    void LinkDirectory(string source, string destination);
}
