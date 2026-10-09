using System.Text.Json;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Grava <c>tccloud-server.json</c> na raiz da pasta da instância (que o
///     container monta em <c>/data</c>). Escrita atômica (temporário + troca)
///     para o mod nunca ler um arquivo pela metade, e permissão só do dono no
///     Linux: o arquivo tem uma chave que grava nos itens de todos os jogadores
///     da nuvem.
/// </summary>
public sealed class CloudServerFiles(IInstanceMaterializer materializer) : ICloudServerFiles
{
    public const string FileName = "tccloud-server.json";

    public async Task WriteAsync(Guid gameServerId, Uri cloudUrl, string key, CancellationToken ct)
    {
        var folder = materializer.GetInstancePath(gameServerId);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, FileName);
        var temp = target + ".tmp";

        var json = JsonSerializer.Serialize(new { url = cloudUrl.ToString().TrimEnd('/'), key });
        await File.WriteAllTextAsync(temp, json, ct);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temp, target, true);
    }

    public Task DeleteAsync(Guid gameServerId, CancellationToken ct)
    {
        var target = Path.Combine(materializer.GetInstancePath(gameServerId), FileName);
        if (File.Exists(target))
            File.Delete(target);
        return Task.CompletedTask;
    }
}
