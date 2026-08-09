using System.Text;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

public sealed class ReadOverride(IModpackRepository repository, IBlobStore blobStore)
{
    /// <summary>
    ///     Acima disto não abrimos no editor. O conteúdo viaja pelo circuito
    ///     SignalR até o Monaco: um arquivo de megabytes trava a aba do admin e
    ///     pode derrubar o circuito — e ninguém edita um arquivo desse tamanho à
    ///     mão de qualquer forma.
    /// </summary>
    private const long MaxEditableBytes = 512 * 1024;

    /// <summary>Quanto se olha para decidir se é binário.</summary>
    private const int SniffBytes = 8 * 1024;

    public async Task<Result<OverrideContent>> HandleAsync(Guid versionId, string path, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result<OverrideContent>.Fail("Versão não encontrada.");

        var file = version.Files.FirstOrDefault(f =>
            f.Origin == ModFileOrigin.Override
            && f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return Result<OverrideContent>.Fail("Arquivo não encontrado.");

        // Grande demais: nem chega a abrir o blob.
        if (file.SizeBytes > MaxEditableBytes)
            return Result<OverrideContent>.Success(OverrideContent.NotEditable(file, "Arquivo grande demais para editar."));

        // Meio mega no máximo, então carregar em memória é barato — e evita
        // decodificar por partes, que partiria um caractere UTF-8 ao meio.
        await using var stream = await blobStore.OpenAsync(file.Sha256, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);

        // Um pack traz PNG, .jar e .zip dentro dos overrides. Lidos como texto
        // viram lixo de megabytes no editor. O byte zero é o sinal clássico de
        // binário — nenhum texto UTF-8 válido o contém.
        if (bytes[..Math.Min(SniffBytes, bytes.Length)].IndexOf((byte)0) >= 0)
            return Result<OverrideContent>.Success(OverrideContent.NotEditable(file, "Arquivo binário."));

        var text = Encoding.UTF8.GetString(bytes);
        return Result<OverrideContent>.Success(new OverrideContent(text, false, file.SizeBytes, file.Sha256));
    }
}

/// <summary>
///     Conteúdo de um override para exibição. <see cref="Text" /> é nulo quando o
///     arquivo não é editável — aí a UI mostra o motivo e oferece o download em
///     vez de despejar bytes no editor.
/// </summary>
public sealed record OverrideContent(string? Text, bool IsBinary, long SizeBytes, string Sha256)
{
    public string? Reason { get; private init; }

    public static OverrideContent NotEditable(ModpackFile file, string reason) =>
        new(null, true, file.SizeBytes, file.Sha256) { Reason = reason };
}
