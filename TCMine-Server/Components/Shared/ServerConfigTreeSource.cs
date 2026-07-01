using TCMine_Server.Infrastructure.ServerInstances;
using TCMine_Server.Components.Pages.Admin.Modpacks;

namespace TCMine_Server.Components.Shared;

/// <summary>
/// <see cref="IFileTreeSource"/> das configs de uma instância de servidor — adapta o
/// <see cref="ServerInstanceService"/> para o <see cref="FileTreeEditor"/> (mesma árvore + Monaco dos
/// overrides). Permite criar/apagar/editar arquivos de texto no diretório da instância; mover/enviar
/// ficam desligados (não fazem sentido para as configs geradas).
/// </summary>
public sealed class ServerConfigTreeSource(ServerInstanceService service, Guid instanceId) : IFileTreeSource
{
    public string LoadingMessage => "Carregando configurações…";
    public bool CanCreate => true;
    public bool CanUpload => false;
    public bool CanMove => false;
    public bool CanDelete => true;

    public IReadOnlyList<FileTreeNode> ListChildren(string folder)
    {
        return service.ListConfigChildren(instanceId, folder)
            .Select(n => new FileTreeNode(n.Path, n.Name, n.IsFolder))
            .ToList();
    }

    public bool IsText(string path)
    {
        return service.IsConfigText(path);
    }

    public string FileIcon(string name)
    {
        return OverrideTreeBuilder.FileIcon(name);
    }

    public Task<string?> ReadAsync(string path)
    {
        return service.ReadConfigAsync(instanceId, path);
    }

    public Task WriteAsync(string path, string content)
    {
        return service.WriteConfigAsync(instanceId, path, content);
    }

    public Task CreateAsync(string path)
    {
        return service.CreateConfigAsync(instanceId, path);
    }

    public Task UploadAsync(string fileName, Stream content)
    {
        throw new NotSupportedException();
    }

    public Task DeleteFileAsync(string path)
    {
        return service.DeleteConfigAsync(instanceId, path);
    }

    public Task MoveAsync(string sourcePath, string targetFolder, bool isFolder)
    {
        throw new NotSupportedException();
    }
}
