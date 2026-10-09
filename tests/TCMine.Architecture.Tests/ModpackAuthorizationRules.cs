using System.Reflection;
using NetArchTest.Rules;
using TCMine.Server.Application;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Architecture.Tests;

/// <summary>
///     Autorização dos casos de uso de modpack. Mesma regra e mesmo motivo da
///     <see cref="AuthorizationRules" /> (servidor): a permissão mora no caso de
///     uso, não na borda.
///     Ao contrário do servidor, aqui a lista de pendências NÃO está vazia —
///     este é o começo do modelo de ownership, não o fim dele. Criar, apagar,
///     renomear, trocar a capa, publicar uma versão e gerenciar editores já
///     checam <see cref="Application.Security.ModpackAccessPolicy" />; o resto
///     do dia a dia de editar um pack (versões, overrides, ingestão de mods,
///     novidades) ainda não. Cada nome abaixo é uma porta que qualquer
///     autenticado ainda abre em qualquer modpack — a mesma lacuna que a
///     auditoria original encontrou, só que agora documentada e travada em
///     teste em vez de silenciosa.
///     As duas listas foram conferidas contra o nome REAL de cada tipo (não o
///     nome do arquivo) — IngestionRequest.cs, por exemplo, declara
///     ModIngestionItem, e um record só vira invisível para o NetArchTest se
///     também estiver aqui, porque record é classe.
/// </summary>
public class ModpackAuthorizationRules
{
    private const string Namespace = "TCMine.Server.Application.Modpacks";

    private static readonly Assembly Application =
        typeof(AssemblyMarker).Assembly;

    /// <summary>
    ///     Dívida declarada, não permissão. Ver o cabeçalho da classe.
    ///     Antes de tirar um nome daqui: injete <c>ICurrentUserScope</c> no caso
    ///     de uso e decida com <c>ModpackAuthorization.RequireAsync</c> +
    ///     <c>ModpackAccessPolicy</c> — o padrão está em
    ///     <c>DeleteModpack</c>/<c>UpdateModpack</c>/<c>SetModpackIcon</c>/
    ///     <c>PublishModpackVersion</c>. Alguns destes nomes podem, depois de
    ///     olhados de perto, pertencer a <see cref="EfeitosInternos" /> em vez
    ///     de ganhar autorização própria — não foram classificados um a um
    ///     ainda.
    /// </summary>
    private static readonly string[] PendentesDeAutorizacao =
    [
        "AddManualFile", "ArchiveModpackVersion", "ChangeFileSide", "CheckModpackVersionUpdates",
        "CheckUpstreamUpdate", "CloneVersion", "CompleteFromServerPack", "CreateModpackVersion",
        "CreateNews", "DeleteModpackVersion", "DeleteNews", "DeleteOverride", "MoveOverride",
        "QueueIngestion", "ReadOverride", "RemoveModpackFile", "RestoreModpackVersion",
        "RetryModResolution", "SaveOverride", "UndoOverrideMove", "UpdateFromUpstream",
        "UpdateModpackVersion", "UpdateNews"
    ];

    /// <summary>
    ///     Efeitos internos, orquestradores de background, e tipos que não são
    ///     casos de uso: records/DTOs de payload (um record é classe, e por isso
    ///     o NetArchTest os vê também), classes estáticas de lógica pura, e
    ///     serviços acionados por quem já autorizou ou por um job, nunca por um
    ///     usuário na hora. Autorizar aqui seria ERRADO ou simplesmente não se
    ///     aplica.
    /// </summary>
    private static readonly string[] EfeitosInternos =
    [
        // Orquestração/background — quem chama já autorizou, ou é um job.
        "BackfillServerPacks", "ImportScheduler", "IngestionScheduler", "ModpackIngestionService",
        "OverrideUndoService", "RecoverInterruptedImports", "RecoverInterruptedIngestions",

        // Lógica pura, sem usuário nenhum na hora.
        "IngestionWorkPlanner", "LoaderVersionRange", "UpstreamMerge",

        // Records/DTOs de payload ou retorno — dado, não caso de uso. Tipo
        // aninhado (privado ou não) aparece qualificado com "/", não pelo nome
        // solto — NetArchTest enumera até os aninhados privados.
        "AddManualFileCommand", "CreateModpackCommand", "CreateModpackVersionCommand",
        "ModIngestionItem", "ModpackIngestionService/Counters", "ModpackIngestionService/ResolveOutcome",
        "ModpackIngestionService/IngestedFile",
        "ModUpdateInfo", "OverrideContent", "OverrideUndoService/UndoEntry", "QueueIngestionCommand",
        "ServerPackFillResult", "UpstreamConflictKind", "UpstreamMergePlan", "UpstreamModChange",
        "UpstreamModConflict", "UpstreamOverridePlan", "UpstreamSnapshot", "UpstreamUpdateResult",
        "UpstreamUpdateStatus",

        // Serialização — infraestrutura de JSON, não caso de uso.
        "SnapshotJsonContext"
    ];

    [Fact]
    public void Caso_de_uso_de_modpack_novo_nasce_autorizando()
    {
        var semAutorizacao = CasosDeUsoSemAutorizacao();

        var novos = semAutorizacao.Except(PendentesDeAutorizacao).Except(EfeitosInternos).ToArray();

        novos.ShouldBeEmpty(
            $"Estes casos de uso não consultam ICurrentUserScope: {string.Join(", ", novos)}. "
            + "Injete o escopo e decida pelo ModpackAccessPolicy antes de agir — ou, se for "
            + "dívida consciente, acrescente o nome a PendentesDeAutorizacao explicando por quê.");
    }

    [Fact]
    public void Lista_de_pendencias_nao_guarda_nome_morto()
    {
        var semAutorizacao = CasosDeUsoSemAutorizacao();

        var mortos = PendentesDeAutorizacao
            .Concat(EfeitosInternos)
            .Except(semAutorizacao)
            .ToArray();

        mortos.ShouldBeEmpty(
            $"Estes nomes estão em PendentesDeAutorizacao ou EfeitosInternos mas já autorizam "
            + $"(ou não existem mais): {string.Join(", ", mortos)}. Remova-os da lista.");
    }

    private static string[] CasosDeUsoSemAutorizacao()
    {
        var casosDeUso = Types.InAssembly(Application)
            .That()
            .ResideInNamespace(Namespace)
            .And()
            .AreClasses();

        casosDeUso.GetTypes().ShouldNotBeEmpty(
            $"Nenhum tipo em {Namespace}: o namespace mudou e esta regra parou de olhar.");

        var result = casosDeUso
            .Should()
            .HaveDependencyOn(typeof(ICurrentUserScope).FullName)
            .GetResult();

        return [.. (result.FailingTypeNames ?? []).Select(NomeSimples)];
    }

    private static string NomeSimples(string nomeCompleto) =>
        nomeCompleto[(nomeCompleto.LastIndexOf('.') + 1)..];
}
