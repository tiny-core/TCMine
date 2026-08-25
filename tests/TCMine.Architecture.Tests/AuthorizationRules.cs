using System.Reflection;
using NetArchTest.Rules;
using TCMine.Server.Application.Abstractions;
using ArchResult = NetArchTest.Rules.TestResult;

namespace TCMine.Architecture.Tests;

/// <summary>
///     Autorização dos casos de uso de servidor.
///     A regra que queremos é "todo caso de uso decide se o usuário pode antes de
///     agir", e o lugar dela é o caso de uso — não a borda. A borda é plural
///     (endpoint HTTP, hub SignalR, componente Blazor) e cada borda nova esquece
///     de novo: foi o que aconteceu entre o MainHub, que checava o papel, e o
///     download de backup, que não checava.
///     Hoje quase nenhum caso de uso cumpre isso. Em vez de fingir que cumpre, a
///     regra carrega a lista das pendências — e trava nas duas direções, para a
///     lista não virar mentira: caso de uso novo não entra sem autorizar, e nome
///     que já foi resolvido não fica apodrecendo aqui.
/// </summary>
public class AuthorizationRules
{
    private const string Namespace = "TCMine.Server.Application.Servers";

    private static readonly Assembly Application =
        typeof(Server.Application.AssemblyMarker).Assembly;

    /// <summary>
    ///     Casos de uso que ainda NÃO consultam o papel do usuário.
    ///     Não é lista de perdão permanente: é dívida declarada, com data. Hoje
    ///     ela não protege ninguém porque a instalação só admite um usuário — o
    ///     CreateFirstAdmin recusa o segundo e nada no código cria Membership.
    ///     No dia em que entrar o fluxo de convite, cada nome aqui vira uma porta
    ///     aberta ao mesmo tempo: um Moderator convidado para moderar o chat
    ///     poderia parar servidores, apagar backups e restaurar mundo por cima.
    ///     Esvaziar esta lista faz parte de entregar aquele fluxo, não depois.
    /// </summary>
    private static readonly string[] PendentesDeAutorizacao = [];

    /// <summary>
    ///     Efeitos internos, e não pontos de entrada. Não é dívida: autorizar
    ///     aqui seria ERRADO.
    ///     O SyncServerWhitelist é acionado por casos de uso que já decidiram —
    ///     resgatar convite, remover membro, subir o servidor —, e no primeiro
    ///     deles o jogador ainda NÃO é membro do servidor. Exigir
    ///     CanManageMembers faria o próprio resgate do convite falhar.
    ///     A lista é curta de propósito. Se ela crescer, o cheiro é de que a
    ///     fronteira entre "caso de uso" e "efeito" se perdeu, e alguém está
    ///     usando-a para fugir da regra em vez de descrever o desenho.
    /// </summary>
    private static readonly string[] EfeitosInternos = ["SyncServerWhitelist"];

    [Fact]
    public void Caso_de_uso_de_servidor_novo_nasce_autorizando()
    {
        var semAutorizacao = CasosDeUsoSemAutorizacao();

        var novos = semAutorizacao.Except(PendentesDeAutorizacao).Except(EfeitosInternos).ToArray();

        novos.ShouldBeEmpty(
            $"Estes casos de uso não consultam ICurrentUserScope: {string.Join(", ", novos)}. "
            + "Injete o escopo e decida pelo ServerAccessPolicy antes de agir — ou, se for "
            + "dívida consciente, acrescente o nome a PendentesDeAutorizacao explicando por quê.");
    }

    [Fact]
    public void Lista_de_pendencias_nao_guarda_nome_morto()
    {
        var semAutorizacao = CasosDeUsoSemAutorizacao();

        // O outro sentido da regra. Sem ele a lista envelhece em silêncio: alguém
        // resolve um caso de uso, esquece de tirá-lo daqui, e a próxima pessoa lê
        // uma dívida que já não existe — ou pior, confia na lista para saber o
        // que falta e deixa de fora o que sobrou.
        var mortos = PendentesDeAutorizacao
            .Concat(EfeitosInternos)
            .Except(semAutorizacao)
            .ToArray();

        mortos.ShouldBeEmpty(
            $"Estes nomes estão em PendentesDeAutorizacao ou EfeitosInternos mas já autorizam "
            + $"(ou não existem mais): {string.Join(", ", mortos)}. Remova-os da lista.");
    }

    /// <summary>
    ///     Nomes simples dos casos de uso do namespace que não referenciam a porta
    ///     de identidade. Sai do próprio NetArchTest — os "reprovados" da regra
    ///     "deve depender de ICurrentUserScope" são exatamente as pendências.
    ///     Uma fonte só para as duas direções do teste: se fossem duas (IL aqui,
    ///     reflexão ali), elas discordariam e a falha seria incompreensível.
    /// </summary>
    private static string[] CasosDeUsoSemAutorizacao()
    {
        var casosDeUso = Types.InAssembly(Application)
            .That()
            .ResideInNamespace(Namespace)
            .And()
            .AreClasses();

        // Sanidade antes da regra: um namespace renomeado faria o filtro casar
        // com zero tipos e o teste passaria sem verificar nada — o falso positivo
        // silencioso que o csproj deste projeto adverte.
        casosDeUso.GetTypes().ShouldNotBeEmpty(
            $"Nenhum tipo em {Namespace}: o namespace mudou e esta regra parou de olhar.");

        ArchResult resultado = casosDeUso
            .Should()
            .HaveDependencyOn(typeof(ICurrentUserScope).FullName)
            .GetResult();

        // FailingTypeNames vem null quando ninguém reprova.
        return [.. (resultado.FailingTypeNames ?? []).Select(NomeSimples)];
    }

    private static string NomeSimples(string nomeCompleto) =>
        nomeCompleto[(nomeCompleto.LastIndexOf('.') + 1)..];
}
