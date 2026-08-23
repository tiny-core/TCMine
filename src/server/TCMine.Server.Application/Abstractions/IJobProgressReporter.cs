namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Canal por onde o trabalho em background conta o que está fazendo.
///     Existe porque sondar o banco a cada 2s dá um progresso grosseiro e
///     atrasado: durante o download de 480 mods a contagem de arquivos só muda
///     quando cada um termina, e nada diz "baixando X de Y" ou "gravando
///     overrides". A UI assina o registro e recebe empurrão, sem polling.
///     O estado vive num singleton do processo, então sobrevive à navegação: o
///     admin pode sair da página e voltar sem perder o acompanhamento.
/// </summary>
public interface IJobProgressReporter
{
    /// <summary>
    ///     Publica o estado atual de um trabalho. <paramref name="scopeId" /> é a
    ///     versão (ingestão) ou o id do job de importação, que ainda não tem
    ///     versão quando começa.
    /// </summary>
    void Report(Guid scopeId, JobProgress progress);

    /// <summary>Encerra o acompanhamento — o trabalho terminou (bem ou mal).</summary>
    void Complete(Guid scopeId, string? error = null);

    /// <summary>
    ///     Já há trabalho em curso neste escopo?
    ///     Existe para o caso de uso poder RECUSAR o segundo pedido. Desabilitar
    ///     o botão na tela não basta: o admin fecha o diálogo, o trabalho segue
    ///     em background, e o próximo clique dispara outro — dois jobs iguais
    ///     consumindo a mesma cota de API, o que já aconteceu.
    /// </summary>
    bool IsRunning(Guid scopeId);
}

/// <summary>
///     Um retrato do que o job está fazendo agora.
///     <paramref name="Total" /> zero significa que não dá para saber o tamanho
///     ainda (ex.: baixando o zip do pack) — a UI mostra barra indeterminada.
/// </summary>
public sealed record JobProgress(string Title, string Step, int Done = 0, int Total = 0)
{
    /// <summary>
    ///     Dependências transitivas processadas. Ficam FORA de Done/Total de
    ///     propósito: o total do pack é conhecido no início e não pode subir
    ///     enquanto baixa — uma barra cujo denominador cresce não informa nada.
    ///     As dependências aparecem à parte, como "+N dependências".
    /// </summary>
    public int Dependencies { get; init; }

    public bool IsDeterminate => Total > 0;

    public double Percent => Total > 0 ? Math.Clamp(Done * 100d / Total, 0, 100) : 0;
}
