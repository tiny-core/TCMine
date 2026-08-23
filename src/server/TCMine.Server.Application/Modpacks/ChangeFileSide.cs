using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Troca o lado de um arquivo de um rascunho.
///     Existe porque nenhuma origem sabe responder isto de forma confiável. O
///     manifest de um pack CurseForge não declara lado nenhum; as tags
///     Client/Server da API faltam na maioria dos arquivos; e o
///     <c>neoforge.mods.toml</c> não tem campo de lado por mod — o Colorwheel,
///     que serve só para usar shaders no cliente, declara todas as dependências
///     como BOTH. Sem uma fonte, o TCMine chuta "os dois", e mods de cliente vão
///     parar no servidor.
///     O server pack do autor resolve quando existe. Isto resolve quando não
///     existe, e resolve para qualquer loader: quem sabe é o admin.
/// </summary>
public sealed class ChangeFileSide(IModpackRepository repository)
{
    public async Task<Result> HandleAsync(
        Guid versionId, Guid fileId, FileSide side, CancellationToken ct)
    {
        var version = await repository.GetVersionAsync(versionId, ct);
        if (version is null)
            return Result.Fail("Versão não encontrada.");

        if (version.State is not ModpackVersionState.Draft)
            return Result.Fail("Só é possível editar uma versão em rascunho.");

        var arquivo = version.Files.FirstOrDefault(f => f.Id == fileId);
        if (arquivo is null)
            return Result.Fail("Arquivo não encontrado nesta versão.");

        if (arquivo.Side == side)
            return Result.Success();

        // Gravação estreita: uma coluna de uma linha. Passar pelo
        // UpdateVersionAsync reanexaria o grafo inteiro — num pack importado são
        // milhares de arquivos remarcados para escrever um enum.
        await repository.SetFileSideAsync(versionId, fileId, side, ct);
        return Result.Success();
    }
}
