using TCMine.Server.Application.Public;

namespace TCMine.Server.Application.Tests.Public;

/// <summary>
///     A página pública mostrava o endereço de todo servidor no ar a qualquer
///     visitante. Ele só sai para quem tem acesso aprovado; o modelo público
///     nem o carrega, para nenhuma tela futura o poder mostrar por engano.
/// </summary>
public sealed class PublicCatalogPrivacyTests
{
    [Fact]
    public void O_servidor_publico_nao_carrega_endereco() =>
        typeof(PublicServerView).GetProperties()
            .Select(p => p.Name)
            .ShouldNotContain(name => name.Contains("Address", StringComparison.OrdinalIgnoreCase)
                                      || name.Contains("Ip", StringComparison.Ordinal)
                                      || name.Contains("Port", StringComparison.OrdinalIgnoreCase));
}
