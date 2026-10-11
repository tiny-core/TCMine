namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Atualiza o próprio launcher, a partir do feed que o servidor indica.
///     Está atrás de porta porque o mecanismo é do empacotamento — o Velopack só
///     faz alguma coisa numa build instalada por ele — e porque o que decide SE
///     atualizar é regra de produto, testável sem nada disso.
/// </summary>
public interface ILauncherUpdater
{
    /// <summary>
    ///     Procura, baixa e aplica. Devolve verdadeiro quando vai reiniciar.
    ///     Executar a partir do código-fonte não é erro: sem instalação feita
    ///     pelo empacotador não há o que substituir, e a resposta certa é não
    ///     fazer nada em silêncio.
    /// </summary>
    Task<bool> UpdateAsync(Uri feedUrl, CancellationToken ct);

    /// <summary>
    ///     Só procura. Devolve a versão que está à espera no feed, ou nulo quando
    ///     não há nada (ou não deu para saber — as duas coisas pedem o mesmo:
    ///     seguir com a versão atual).
    ///     Separado do <see cref="UpdateAsync" /> porque no meio de uma sessão
    ///     reiniciar não é decisão do launcher: ele avisa, e o jogador escolhe
    ///     quando.
    /// </summary>
    Task<string?> FindUpdateAsync(Uri feedUrl, CancellationToken ct);
}
