using System.Collections.Concurrent;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Pilha de undo das movimentações de override, por versão, só em memória.
///     Guarda o mínimo para reverter — o path anterior de cada arquivo movido, não
///     a operação inteira. Morre quando a versão é publicada (limpa no
///     PublishModpackVersion) ou quando o TCMine reinicia (é trabalho de sessão).
///     Singleton: partilhado entre os requests do editor.
/// </summary>
public sealed class OverrideUndoService
{
    private readonly ConcurrentDictionary<Guid, Stack<UndoEntry>> _byVersion = new();

    public void Record(Guid versionId, Guid fileId, string previousPath)
    {
        var stack = _byVersion.GetOrAdd(versionId, _ => new Stack<UndoEntry>());
        lock (stack) stack.Push(new UndoEntry(fileId, previousPath));
    }

    /// <summary>Tira a última movimentação, ou null se não há nada a desfazer.</summary>
    public UndoEntry? Pop(Guid versionId)
    {
        if (!_byVersion.TryGetValue(versionId, out var stack))
            return null;

        lock (stack) return stack.Count > 0 ? stack.Pop() : null;
    }

    public bool HasUndo(Guid versionId) => _byVersion.TryGetValue(versionId, out var stack) && stack.Count > 0;

    /// <summary>Esvazia o histórico da versão (chamado ao publicar).</summary>
    public void Clear(Guid versionId) => _byVersion.TryRemove(versionId, out _);

    public sealed record UndoEntry(Guid FileId, string PreviousPath);
}
