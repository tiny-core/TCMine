using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts;
using TCMine.Launcher.Infrastructure;
using TCMine.Launcher.Infrastructure.Configuration;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     O tcmine.json vai ao disco e volta.
///     Contra o disco de verdade porque é o disco que quebra: a gravação passa
///     por arquivo temporário e move, e a leitura precisa aceitar exatamente o
///     que a gravação escreveu — em camelCase, pelo contexto compartilhado.
/// </summary>
public sealed class FileLauncherConfigProviderTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "tcmine-teste-" + Guid.NewGuid().ToString("N")[..8]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    private FileLauncherConfigProvider Criar() =>
        new(new LauncherPaths(_raiz), NullLogger<FileLauncherConfigProvider>.Instance);

    [Fact]
    public async Task Sem_arquivo_devolve_nulo_em_vez_de_estourar()
    {
        // O primeiro arranque de qualquer instalação passa por aqui.
        (await Criar().TryLoadAsync(Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Grava_e_le_de_volta()
    {
        var provider = Criar();

        var config = new LauncherConfig
        {
            Schema = 1,
            ServerUrl = new Uri("https://modpacks.exemplo/"),
            AzureClientId = "client",
            DisplayName = "Servidor de Teste"
        };

        await provider.SaveAsync(config, Ct);

        (await provider.TryLoadAsync(Ct)).ShouldBe(config);
    }

    [Fact]
    public async Task Arquivo_corrompido_e_tratado_como_ausente()
    {
        // Antivírus em quarentena, queda no meio da escrita. A recuperação é a
        // tela de pareamento manual, e ela só aparece se a leitura não explodir.
        Directory.CreateDirectory(_raiz);
        await File.WriteAllTextAsync(Path.Combine(_raiz, "tcmine.json"), "{ isto não é json", Ct);

        (await Criar().TryLoadAsync(Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Configuracao_invalida_nao_e_gravada()
    {
        // HTTP puro fora de localhost: o id_token da Microsoft trafega nessa
        // conexão, e gravar significaria voltar a usá-la no próximo arranque.
        var provider = Criar();

        var invalida = new LauncherConfig
        {
            Schema = 1, ServerUrl = new Uri("http://modpacks.exemplo/"), AzureClientId = "client"
        };

        await Should.ThrowAsync<ArgumentException>(async () => await provider.SaveAsync(invalida, Ct));

        File.Exists(Path.Combine(_raiz, "tcmine.json")).ShouldBeFalse();
    }
}
