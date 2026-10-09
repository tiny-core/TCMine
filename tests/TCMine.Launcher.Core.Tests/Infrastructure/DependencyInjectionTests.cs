using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Infrastructure;

namespace TCMine.Launcher.Core.Tests.Infrastructure;

/// <summary>
///     Tudo o que o launcher regista consegue ser construído.
///     Escrito depois de uma falha que TODA a suíte deixou passar: um caso de uso
///     ganhou uma dependência nova, a interface dela não foi registada, e nada
///     acusou — porque os testes montam os casos de uso à mão, com fakes, e nunca
///     pedem nada ao contentor. O launcher compilava, os 764 testes passavam, e a
///     tela de Instâncias rebentava ao abrir.
///     É a única verificação aqui que exercita a COMPOSIÇÃO em vez do
///     comportamento, e é por isso que ela vale: um registo em falta não é um bug
///     de lógica que um teste de unidade apanhe, é uma peça que ninguém ligou.
/// </summary>
public sealed class DependencyInjectionTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), $"tcmine-di-{Guid.CreateVersion7():N}");

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }

    [Fact]
    public async Task Tudo_o_que_o_launcher_regista_resolve()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddLauncherInfrastructure(_raiz);
        services.AddLauncherCore();

        // DisposeAsync e não using: o SignalRServerConnection só implementa
        // IAsyncDisposable, e o contentor recusa-se a ser descartado em
        // síncrono quando tem um desses lá dentro.
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            // O contentor valida sozinho o que consegue: dependências em falta e
            // singletons a capturar scoped, que é a outra classe de erro que só
            // aparece em runtime.
            ValidateOnBuild = true, ValidateScopes = true
        });

        await using var escopo = provider.CreateAsyncScope();

        // Só o que é nosso: resolver o mundo inteiro traria tipos internos do
        // logging e do HttpClient, cuja construção não é responsabilidade deste
        // teste.
        var nossos = services
            .Select(d => d.ServiceType)
            .Where(t => t.Namespace?.StartsWith("TCMine", StringComparison.Ordinal) is true)
            .Distinct()
            .ToArray();

        nossos.ShouldNotBeEmpty("o registo do launcher ficou vazio — algo mudou de nome");

        foreach (var tipo in nossos)
            Should.NotThrow(() => escopo.ServiceProvider.GetRequiredService(tipo), $"não resolve: {tipo}");
    }
}
