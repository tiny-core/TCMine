namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Consumo de recursos de uma instância, amostrado do runtime de containers.
///     Fica separado do <see cref="IServerOrchestrator" /> de propósito: o
///     orquestrador manda no ciclo de vida (criar, parar, remover) e é chamado em
///     resposta ao admin; isto aqui é leitura periódica de telemetria, chamada por
///     um coletor em background. Misturar os dois faria a tela de servidores
///     depender de uma porta que só o painel usa.
/// </summary>
public interface IContainerStats
{
    /// <summary>
    ///     Um retrato instantâneo. Null quando o container não existe ou está
    ///     parado — não é erro, é o caso comum de um servidor desligado.
    /// </summary>
    Task<ContainerSample?> SampleAsync(Guid gameServerId, CancellationToken ct);
}

/// <summary>
///     Uma amostra de consumo. Percentuais já calculados aqui porque a conta do
///     Docker (delta de ciclos de CPU entre duas leituras) é detalhe do adaptador,
///     não algo que a UI deva saber fazer.
/// </summary>
public sealed record ContainerSample(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryLimitBytes)
{
    public double MemoryPercent =>
        MemoryLimitBytes > 0 ? Math.Clamp(MemoryUsedBytes * 100d / MemoryLimitBytes, 0, 100) : 0;
}
