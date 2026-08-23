using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     Cancelar um trabalho em curso.
///     O cancelamento vive no mesmo singleton do progresso porque o admin que
///     mandou parar pode ter saído da página — e um botão que só funciona na
///     tela onde o trabalho começou não serve para uma importação de vinte
///     minutos.
/// </summary>
public sealed class JobCancellationTests
{
    [Fact]
    public void Trabalho_sem_registro_nao_e_cancelavel()
    {
        // O botão não pode aparecer para um trabalho que ninguém consegue parar:
        // seria oferecer o que não acontece.
        var registry = new JobProgressRegistry();

        registry.IsCancellable(Guid.CreateVersion7()).ShouldBeFalse();
    }

    [Fact]
    public void Cancelar_dispara_o_token_do_trabalho()
    {
        var registry = new JobProgressRegistry();
        var scope = Guid.CreateVersion7();

        var token = registry.BeginCancellable(scope, CancellationToken.None);

        registry.IsCancellable(scope).ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeFalse();

        registry.Cancel(scope);

        token.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Cancelar_um_trabalho_nao_atinge_os_outros()
    {
        // A fila continua: parar uma importação não pode derrubar as seguintes.
        var registry = new JobProgressRegistry();
        var um = Guid.CreateVersion7();
        var outro = Guid.CreateVersion7();

        var tokenUm = registry.BeginCancellable(um, CancellationToken.None);
        var tokenOutro = registry.BeginCancellable(outro, CancellationToken.None);

        registry.Cancel(um);

        tokenUm.IsCancellationRequested.ShouldBeTrue();
        tokenOutro.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void Desligar_a_aplicacao_cancela_o_trabalho()
    {
        // Ligado ao desligamento para um deploy não ficar esperando por uma
        // ingestão de quinze minutos.
        using var aplicacao = new CancellationTokenSource();
        var registry = new JobProgressRegistry();

        var token = registry.BeginCancellable(Guid.CreateVersion7(), aplicacao.Token);

        aplicacao.Cancel();

        token.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Terminar_fecha_o_cancelamento()
    {
        // Depois de terminado o botão some. Sem isto o admin cancelaria um
        // trabalho que já acabou e nada aconteceria.
        var registry = new JobProgressRegistry();
        var scope = Guid.CreateVersion7();

        registry.BeginCancellable(scope, CancellationToken.None);
        registry.EndCancellable(scope);

        registry.IsCancellable(scope).ShouldBeFalse();
    }

    [Fact]
    public void Um_trabalho_novo_no_mesmo_escopo_comeca_limpo()
    {
        // Reingerir a mesma versão depois de cancelar: o token velho não pode
        // vazar para o trabalho novo, senão ele nasceria cancelado.
        var registry = new JobProgressRegistry();
        var scope = Guid.CreateVersion7();

        registry.BeginCancellable(scope, CancellationToken.None);
        registry.Cancel(scope);

        var novo = registry.BeginCancellable(scope, CancellationToken.None);

        novo.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void Progresso_e_cancelamento_sao_independentes()
    {
        // IsRunning fala do acompanhamento; IsCancellable, de haver quem pare.
        // Confundir os dois faria a guarda de duplicata barrar trabalho que não
        // está em curso.
        var registry = new JobProgressRegistry();
        var scope = Guid.CreateVersion7();

        registry.BeginCancellable(scope, CancellationToken.None);

        registry.IsRunning(scope).ShouldBeFalse("nada reportou progresso ainda");

        registry.Report(scope, new JobProgress("t", "passo", 0, 0));

        registry.IsRunning(scope).ShouldBeTrue();
    }
}
