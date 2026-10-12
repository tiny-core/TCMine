using TCMine.Contracts.Hubs;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Avisa os launchers de que algo que eles mostram mudou.
///     Chamado pelos repositórios DEPOIS de gravar, e não pelos casos de uso:
///     o repositório é o ponto por onde toda escrita passa — inclusive as que
///     não passam pelo SaveChanges (ExecuteDelete, ExecuteUpdate), que é como
///     modpacks, versões, servidores e novidades são apagados.
///     Síncrono e barato de propósito: só enfileira. Juntar a rajada e enviar é
///     de quem implementa, fora do caminho da gravação — um launcher lento não
///     pode atrasar um "Salvar" no painel.
///     O aviso leva só o tipo e o id. Quem recebe busca de novo pelo hub, com
///     as permissões de sempre: nada do que mudou viaja no aviso.
/// </summary>
public interface IResourceChanges
{
    /// <param name="id">O recurso que mudou; nulo quando são vários, ou quando não se sabe qual.</param>
    void Touch(ResourceKind kind, Guid? id = null);
}

/// <summary>
///     A escrita muda algo que o launcher mostra, e o repositório avisa por
///     <see cref="IResourceChanges" />.
///     Este atributo e o <see cref="NotVisibleToLauncherAttribute" /> existem
///     para OBRIGAR a decisão: a regra de arquitetura reprova qualquer método de
///     escrita dos repositórios que o launcher lê sem um dos dois. Um método novo
///     que esquecesse o aviso deixaria os launchers com dados velhos até alguém
///     navegar — e nenhum teste de comportamento repararia.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class InvalidatesAttribute(ResourceKind kind) : Attribute
{
    public ResourceKind Kind { get; } = kind;
}

/// <summary>
///     A escrita não muda nada que o launcher mostre. O motivo é obrigatório:
///     é ele que alguém vai ler no dia em que isso deixar de ser verdade.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NotVisibleToLauncherAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
