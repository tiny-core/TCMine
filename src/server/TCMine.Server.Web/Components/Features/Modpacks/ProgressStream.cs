namespace TCMine.Server.Web.Components.Features.Modpacks;

/// <summary>
///     Envolve um stream contando o que passa e avisando quem quiser.
///     Existe para o upload manual: o navegador entrega o arquivo em pedaços, e
///     sem contar os bytes no caminho o admin espera minutos por um .jar de 200 MB
///     sem saber se está subindo. Não dá para perguntar o progresso ao stream do
///     Blazor — só dá para observar o que já foi lido.
/// </summary>
public sealed class ProgressStream(Stream inner, long totalBytes, Action<long, long> onProgress) : Stream
{
    /// <summary>
    ///     Avisa a cada 512 KB, não a cada leitura. O Blazor entrega em blocos
    ///     pequenos, e uma renderização por bloco inundaria o circuito.
    /// </summary>
    private const long ReportEvery = 512 * 1024;

    private long _lastReported;
    private long _read;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => totalBytes;

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await inner.ReadAsync(buffer, cancellationToken);
        Advance(count);
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        Advance(read);
        return read;
    }

    private void Advance(int count)
    {
        if (count <= 0)
            return;

        _read += count;

        // Sempre avisa no fim, mesmo que falte pouco para o próximo bloco —
        // senão a barra congela em 97%.
        if (_read - _lastReported < ReportEvery && _read < totalBytes)
            return;

        _lastReported = _read;
        onProgress(_read, totalBytes);
    }

    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();

        base.Dispose(disposing);
    }
}
