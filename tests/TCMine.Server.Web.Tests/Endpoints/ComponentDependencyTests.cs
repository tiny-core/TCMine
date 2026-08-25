using Microsoft.Extensions.DependencyInjection;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Settings;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Tudo o que componentes Blazor, o MainHub e os serviços de background
///     injetam precisa estar registrado.
///     Existe porque a injeção é resolvida na hora em que o componente renderiza
///     ou o hub é invocado, não no arranque: um caso de uso esquecido no
///     <c>AddTCMineApplication</c> passa por build, testes e health check, e só
///     aparece quando alguém abre a tela — como uma exceção no circuito, que
///     derruba a página inteira em vez de mostrar um erro.
/// </summary>
public sealed class ComponentDependencyTests(AplicacaoDeTeste app) : IClassFixture<AplicacaoDeTeste>
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
        typeof(SendServerCommand),
        typeof(SendTestEmail),
        typeof(StartMailServer),
        typeof(StopMailServer),
        typeof(GetMailServerView),
        typeof(CompleteFromServerPack),

        // Resolvido pelo InterruptedWorkRecovery no arranque, dentro do próprio
        // escopo: um registro faltando ali não impede a app de subir, só faz o
        // preenchimento falhar em silêncio.
        typeof(BackfillServerPacks),
        typeof(ChangeFileSide),
        typeof(IServerWhitelistSync),

        // Portas, e não casos de uso: o IEmailSender é resolvido pela tela de
        // Configurações e as três seguintes pelo MetricsCollector, dentro do
        // próprio escopo a cada coleta. Um registro faltando ali não impede a
        // app de subir — só faz a coleta falhar em silêncio de quinze em quinze
        // segundos.
        typeof(IEmailSender),
        typeof(IMailServerOrchestrator),
        typeof(IPlayerCountSource),
        typeof(IRconClient),
        typeof(IServerHubNotifier),
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
        using var escopo = app.Services.CreateScope();

        // Resolver de verdade, e não só consultar o registro: a falha comum não
        // é o tipo faltar, é uma DEPENDÊNCIA dele faltar — e isso só aparece ao
        // construir o grafo inteiro.
        var servico = escopo.ServiceProvider.GetService(tipo);

        servico.ShouldNotBeNull(
            $"{tipo.Name} é injetado por um componente mas não resolve. "
            + "Registre-o (ou à dependência que falta) em AddTCMineApplication.");
    }
}

/// <summary>
///     Uma aplicação de pé, compartilhada por todos os casos.
///     Subir uma por caso custava vinte e sete arranques com migrations para
///     responder vinte e sete vezes a mesma pergunta — "isto resolve?" —, e a
///     contenção que isso criava fazia OUTROS testes falharem por tempo
///     esgotado, com erros que não tinham nada a ver com eles.
///     A fábrica só é lida aqui, nunca modificada, então compartilhá-la não
///     acopla um caso ao outro.
/// </summary>
public sealed class AplicacaoDeTeste : IDisposable
{
    // A fábrica é interna e não pode vazar por uma classe pública; o que os
    // casos precisam é do provedor, não dela.
    private readonly TcMineAppFactory _factory = new();

    public IServiceProvider Services => _factory.Services;

    public void Dispose() => _factory.Dispose();
}
