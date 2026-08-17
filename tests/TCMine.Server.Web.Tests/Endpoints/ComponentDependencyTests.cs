using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Servers;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Tudo o que componentes Blazor e o MainHub injetam precisa estar
///     registrado.
///     Existe porque a injeção é resolvida na hora em que o componente renderiza
///     ou o hub é invocado, não no arranque: um caso de uso esquecido no
///     <c>AddTCMineApplication</c> passa por build, testes e health check, e só
///     aparece quando alguém abre a tela — como uma exceção no circuito, que
///     derruba a página inteira em vez de mostrar um erro.
/// </summary>
public sealed class ComponentDependencyTests
{
    public static TheoryData<Type> CasosDeUso =>
    [
        typeof(CreateInvite),
        typeof(RedeemInvite),
        typeof(RevokeInvite),
        typeof(RemoveMember),
        typeof(ChangeMemberRole),
        typeof(ListServerAccess),
        typeof(ListAccessibleServers),
        typeof(CreateGameServer),
        typeof(StartGameServer),
        typeof(StopGameServer),
        typeof(DeleteGameServer),
        typeof(UpdateGameServer),
        typeof(ChangeServerVersion),
        typeof(CreateWorldBackup),
        typeof(RestoreWorldBackup),
        typeof(DeleteWorldBackup)
    ];

    [Theory]
    [MemberData(nameof(CasosDeUso))]
    public void Caso_de_uso_injetado_por_componente_resolve(Type tipo)
    {
        using var factory = new TcMineAppFactory();
        using var escopo = factory.Services.CreateScope();

        // Resolver de verdade, e não só consultar o registro: a falha comum não
        // é o tipo faltar, é uma DEPENDÊNCIA dele faltar — e isso só aparece ao
        // construir o grafo inteiro.
        var servico = escopo.ServiceProvider.GetService(tipo);

        servico.ShouldNotBeNull(
            $"{tipo.Name} é injetado por um componente mas não resolve. "
            + "Registre-o (ou à dependência que falta) em AddTCMineApplication.");
    }
}
