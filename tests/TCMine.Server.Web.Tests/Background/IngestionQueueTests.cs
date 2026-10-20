using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     A fila de ingestão por versão (fase 6.5): repetidos viram um, e a versão
///     volta a poder entrar no instante em que o worker a pega — um pedido feito
///     durante a passada precisa de outra passada.
/// </summary>
public sealed class IngestionQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Versao_que_ja_espera_nao_entra_de_novo_e_volta_a_entrar_depois_de_pega()
    {
        var queue = new IngestionQueue();
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        await queue.EnqueueAsync(a, Ct);
        await queue.EnqueueAsync(a, Ct); // mesmo pedido de novo: a passada de "a" pega os dois
        await queue.EnqueueAsync(b, Ct);

        await using var worker = queue.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);
        (await worker.MoveNextAsync()).ShouldBeTrue();
        worker.Current.ShouldBe(a);

        // O worker pegou "a" e está ingerindo; um pedido agora precisa de outra passada.
        await queue.EnqueueAsync(a, Ct);

        (await worker.MoveNextAsync()).ShouldBeTrue();
        worker.Current.ShouldBe(b, "o segundo pedido de antes não virou outra entrada");
        (await worker.MoveNextAsync()).ShouldBeTrue();
        worker.Current.ShouldBe(a);
    }

    [Fact]
    public async Task Fila_cheia_faz_quem_pede_esperar_em_vez_de_crescer_sem_fim()
    {
        var queue = new IngestionQueue();
        for (var i = 0; i < IngestionQueue.Capacity; i++)
            await queue.EnqueueAsync(Guid.CreateVersion7(), Ct);

        // Uma além do teto espera a vaga. Cancelada, ela não fica marcada como
        // "na fila": o próximo pedido dessa versão tem de conseguir entrar.
        var extra = Guid.CreateVersion7();
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        espera.CancelAfter(TimeSpan.FromMilliseconds(100));
        var cancelado = false;
        try
        {
            await queue.EnqueueAsync(extra, espera.Token);
        }
        catch (OperationCanceledException)
        {
            cancelado = true;
        }

        cancelado.ShouldBeTrue("com a fila cheia, quem pede espera a vaga");

        await using var worker = queue.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);
        (await worker.MoveNextAsync()).ShouldBeTrue(); // abre uma vaga
        await queue.EnqueueAsync(extra, Ct);

        for (var i = 1; i < IngestionQueue.Capacity; i++)
            (await worker.MoveNextAsync()).ShouldBeTrue();
        (await worker.MoveNextAsync()).ShouldBeTrue();
        worker.Current.ShouldBe(extra);
    }
}
