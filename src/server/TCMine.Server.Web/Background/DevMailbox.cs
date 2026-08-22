using System.Collections.Concurrent;

namespace TCMine.Server.Web.Background;

/// <summary>
///     As mensagens capturadas pelo SMTP de desenvolvimento.
///     Em memória e com teto: some quando o processo reinicia, e é isso mesmo —
///     uma caixa de teste que sobrevivesse a reinícios seria um arquivo de
///     e-mails que ninguém pediu, com links de recuperação de senha dentro.
/// </summary>
public sealed class DevMailbox
{
    private readonly ConcurrentQueue<CapturedEmail> _mensagens = new();

    public int Capacity { get; init; } = 50;

    /// <summary>Da mais recente para a mais antiga — é assim que se lê caixa de entrada.</summary>
    public IReadOnlyList<CapturedEmail> Recent() => [.. _mensagens.Reverse()];

    public int Count => _mensagens.Count;

    public void Add(CapturedEmail email)
    {
        _mensagens.Enqueue(email);

        while (_mensagens.Count > Capacity && _mensagens.TryDequeue(out _))
        {
            // Descarta a mais antiga até caber.
        }
    }

    public void Clear()
    {
        while (_mensagens.TryDequeue(out _))
        {
            // Esvazia.
        }
    }
}

public sealed record CapturedEmail(
    DateTimeOffset ReceivedAt,
    string From,
    string To,
    string Subject,
    string Body);
