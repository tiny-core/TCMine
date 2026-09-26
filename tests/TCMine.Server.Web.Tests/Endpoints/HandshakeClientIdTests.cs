using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Handshake;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     De onde o handshake tira o client ID do Azure.
///     O valor morava só em appsettings, e o preço aparecia depois do deploy: o
///     admin registrava a app no Azure, não tinha onde colar o id, e a correção
///     exigia editar arquivo dentro do container e reiniciar com jogadores
///     conectados. Agora ele vem do painel — mas o arquivo continua valendo como
///     semente, senão toda instalação já configurada perderia o login numa
///     atualização.
/// </summary>
public class HandshakeClientIdTests
{
    private const string DoPainel = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Sem_valor_no_painel_o_appsettings_ainda_vale()
    {
        using var factory = new TcMineAppFactory();
        var cliente = factory.CreateClient();

        var response = await LerAsync(cliente);

        response.AzureClientId.ShouldBe(TcMineAppFactory.ClientIdDoArquivo);
    }

    [Fact]
    public async Task O_valor_do_painel_vence_o_do_appsettings()
    {
        using var factory = new TcMineAppFactory();
        var cliente = factory.CreateClient();

        await GravarNoPainelAsync(factory, DoPainel);

        var response = await LerAsync(cliente);

        // Sem reiniciar o processo: é a razão de o handshake ler do repositório
        // em vez de IOptions, que é fixado no arranque.
        response.AzureClientId.ShouldBe(DoPainel);
    }

    [Fact]
    public async Task Limpar_no_painel_devolve_o_valor_do_appsettings()
    {
        using var factory = new TcMineAppFactory();
        var cliente = factory.CreateClient();

        await GravarNoPainelAsync(factory, DoPainel);
        await GravarNoPainelAsync(factory, null);

        // A precedência é "painel se houver", não "painel para sempre". Um admin
        // que apaga o campo espera voltar ao que o arquivo diz, e não ficar sem
        // login nenhum.
        var response = await LerAsync(cliente);

        response.AzureClientId.ShouldBe(TcMineAppFactory.ClientIdDoArquivo);
    }

    private static async Task<HandshakeResponse> LerAsync(HttpClient cliente)
    {
        var response = await cliente.GetFromJsonAsync<HandshakeResponse>(
            "/api/handshake", TestContext.Current.CancellationToken);

        return response.ShouldNotBeNull();
    }

    private static async Task GravarNoPainelAsync(TcMineAppFactory factory, string? clientId)
    {
        using var escopo = factory.Services.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<ISettingsRepository>();

        var settings = await repositorio.GetAsync(TestContext.Current.CancellationToken);
        settings.AzureClientId = clientId;

        await repositorio.SaveAsync(settings, TestContext.Current.CancellationToken);
    }
}
