using TCMine_Server.Infrastructure.Minecraft;
using TCMine_Server.Components.Pages.Admin.Modpacks;

namespace TCMine_Server.Components.Shared;

/// <summary>
/// <see cref="IFileTreeSource"/> dos overrides de um modpack — adapta o <see cref="ModpackImportService"/>
/// para o <see cref="FileTreeEditor"/>. Suporta todas as operações (criar/enviar/mover/apagar). O
/// histórico/desfazer fica no host (toolbar extra), não aqui.
/// </summary>
public sealed class OverridesTreeSource(ModpackImportService service, Guid modpackId) : IFileTreeSource
{
    public string LoadingMessage => "Carregando overrides…";
    public bool CanCreate => true;
    public bool CanUpload => true;
    public bool CanMove => true;
    public bool CanDelete => true;

    public IReadOnlyList<FileTreeNode> ListChildren(string folder)
    {
        return service.ListOverrideChildren(modpackId, folder)
            .Select(n => new FileTreeNode(n.Path, n.Name, n.IsFolder))
            .ToList();
    }

    public bool IsText(string path)
    {
        return ModpackImportService.IsTextOverride(path);
    }

    public string FileIcon(string name)
    {
        return OverrideTreeBuilder.FileIcon(name);
    }

    public Task<string?> ReadAsync(string path)
    {
        return service.ReadOverrideAsync(modpackId, path);
    }

    public Task WriteAsync(string path, string content)
    {
        return service.WriteOverrideAsync(modpackId, path, content);
    }

    public Task CreateAsync(string path)
    {
        return service.CreateOverrideAsync(modpackId, path);
    }

    public Task UploadAsync(string fileName, Stream content)
    {
        return service.UploadOverrideAsync(modpackId, fileName, content);
    }

    public Task DeleteFileAsync(string path)
    {
        return service.DeleteOverrideAsync(modpackId, path);
    }

    public Task MoveAsync(string sourcePath, string targetFolder, bool isFolder)
    {
        return isFolder
            ? service.MoveOverrideFolderAsync(modpackId, sourcePath, targetFolder)
            : service.MoveOverrideAsync(modpackId, sourcePath, targetFolder);
    }
}
