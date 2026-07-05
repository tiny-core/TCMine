namespace TCMine_Launcher.Infrastructure.Launch;

/// <summary>Escreve a saída do jogo num arquivo e mantém as últimas linhas em memória. Thread-safe.</summary>
internal sealed class GameLogCapture : IDisposable
{
    private const int TailSize = 30;
    private readonly Lock _lock = new();
    private readonly Queue<string> _tail = new();
    private readonly StreamWriter _writer;

    public GameLogCapture(string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        _writer = new StreamWriter(logPath, false) { AutoFlush = true };
        LogPath = logPath;
    }

    public string LogPath { get; }

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _writer.Dispose();
            }
            catch
            {
                /* noop */
            }
        }
    }

    public void Append(string? line)
    {
        if (line is null) return;
        lock (_lock)
        {
            _writer.WriteLine(line);
            _tail.Enqueue(line);
            while (_tail.Count > TailSize) _tail.Dequeue();
        }
    }

    public string[] Tail()
    {
        lock (_lock)
        {
            return _tail.ToArray();
        }
    }
}