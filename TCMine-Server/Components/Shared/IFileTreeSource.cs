namespace TCMine_Server.Components.Shared;

/// <summary>Um nó da árvore de arquivos: caminho relativo, nome de exibição e se é pasta.</summary>
public sealed record FileTreeNode(string Path, string Name, bool IsFolder);

/// <summary>
///     Fonte de dados de uma árvore de arquivos editável — abstrai o backend para o componente compartilhado
///     <c>FileTreeEditor</c> servir tanto os overrides de modpack quanto as configs de um servidor, sem
///     duplicar a árvore/editor. Cada host cria a sua implementação amarrada ao contexto (modpack/instância).
///     As capacidades (<c>Can*</c>) controlam quais ações o toolbar mostra; uma operação não suportada não
///     precisa ser implementada de fato (a UI nem a oferece).
/// </summary>
public interface IFileTreeSource
{
    /// <summary>Mensagem do overlay ao carregar (ex.: "Carregando overrides…").</summary>
    string LoadingMessage { get; }

    bool CanCreate { get; }
    bool CanUpload { get; }
    bool CanMove { get; }
    bool CanDelete { get; }

    /// <summary>Filhos diretos de uma pasta (lazy). <paramref name="folder" /> vazio = raiz.</summary>
    IReadOnlyList<FileTreeNode> ListChildren(string folder);

    /// <summary>É um arquivo de texto editável? (binários abrem só-leitura no editor).</summary>
    bool IsText(string path);

    /// <summary>Ícone (Material) para um arquivo pelo nome.</summary>
    string FileIcon(string name);

    Task<string?> ReadAsync(string path);
    Task WriteAsync(string path, string content);
    Task CreateAsync(string path);
    Task UploadAsync(string fileName, Stream content);
    Task DeleteFileAsync(string path);

    /// <summary>Move um arquivo (<paramref name="isFolder" /> false) ou pasta para a pasta de destino.</summary>
    Task MoveAsync(string sourcePath, string targetFolder, bool isFolder);
}