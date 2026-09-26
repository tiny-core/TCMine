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
}
