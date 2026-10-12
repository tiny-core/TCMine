using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TCMine.Server.Web.Tests.Infrastructure;

/// <summary>
///     A aplicação num socket de verdade, e não no TestServer.
///     Existe porque o cliente do launcher abre a própria conexão: ele cria o
///     HttpClient e o HubConnection por dentro, e não há onde injetar o handler
///     em memória sem furar o desenho para o teste ver. Com Kestrel numa porta
///     efêmera, o cliente é exercido exatamente como na máquina do jogador —
///     socket, cookies e negociação de transporte inclusive.
/// </summary>
internal sealed class RealPortAppFactory : TcMineAppFactory
{
    private IHost? _kestrel;

    /// <summary>
    ///     Endereço real, com a porta que o sistema escolheu.
    ///     Ler o endereço SOBE a aplicação. A fábrica só arranca no primeiro uso
    ///     dos serviços, e o endereço era uma propriedade com valor inicial
    ///     "http://localhost": um teste que o lia antes de semear qualquer coisa
    ///     recebia a porta 80, onde não há ninguém. Foi assim que
    ///     Sem_entrar_o_hub_recusa_a_conexao passou durante muito tempo sem
    ///     nunca falar com o servidor — a ligação falhava por recusa do sistema,
    ///     e o teste lia isso como recusa do hub.
    ///     Lido do próprio Kestrel, e não de um campo preenchido no arranque: não
    ///     há valor guardado que possa estar desatualizado ou vazio.
    /// </summary>
    public Uri Address
    {
        get
        {
            _ = Services;

            var enderecos = _kestrel?.Services
                                .GetRequiredService<IServer>()
                                .Features.Get<IServerAddressesFeature>()
                            ?? throw new InvalidOperationException("O Kestrel do teste não subiu.");

            return new Uri(enderecos.Addresses.First());
        }
    }

    /// <summary>
    ///     Os serviços do Kestrel, que é o host com que o cliente do launcher fala.
    ///     NÃO são os mesmos do <c>Services</c>: o Kestrel é um segundo host, com
    ///     os próprios singletons. O banco é partilhado (mesmo arquivo), por isso
    ///     semear por <c>Services</c> funciona; um aviso pelo hub, não — sairia do
    ///     host errado e nunca chegaria ao launcher.
    /// </summary>
    public IServiceProvider KestrelServices
    {
        get
        {
            _ = Services;

            return _kestrel?.Services ?? throw new InvalidOperationException("O Kestrel do teste não subiu.");
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // A base exige um host com TestServer para o CreateClient dela seguir
        // funcionando; é este que devolvemos. O Kestrel sobe ao lado.
        var emMemoria = builder.Build();

        builder.ConfigureWebHost(web => web.UseKestrel(options =>
        {
            // Loopback explícito, e não ListenLocalhost: o Kestrel recusa porta
            // dinâmica em "localhost" porque o nome resolve para dois endereços
            // e ele não saberia qual porta anunciar.
            options.Listen(IPAddress.Loopback, 0);
        }));

        _kestrel = builder.Build();
        _kestrel.Start();

        return emMemoria;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _kestrel?.Dispose();

        base.Dispose(disposing);
    }
}
