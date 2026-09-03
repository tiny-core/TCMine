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

    /// <summary>Endereço real, com a porta que o sistema escolheu.</summary>
    public Uri Address { get; private set; } = new("http://localhost");

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

        var enderecos = _kestrel.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!;

        Address = new Uri(enderecos.Addresses.First());

        return emMemoria;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _kestrel?.Dispose();

        base.Dispose(disposing);
    }
}
