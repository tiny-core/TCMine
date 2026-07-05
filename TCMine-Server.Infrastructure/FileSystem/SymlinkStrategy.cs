namespace TCMine_Server.Infrastructure.FileSystem;

/// <summary>
///     Estratégia de produção: cria <b>symlinks</b> reais para arquivos e pastas. Custo de disco zero —
///     o conteúdo continua só no cache compartilhado. É o caminho usado dentro do container Linux, onde
///     symlinks não exigem privilégio e são resolvidos pelo mesmo filesystem do host.
/// </summary>
public sealed class SymlinkStrategy : ILinkStrategy
{
    public string Name => "Symlink";

    public void LinkFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Remove um link/arquivo anterior para o symlink poder ser recriado de forma idempotente
        if (File.Exists(destination)) File.Delete(destination);
        File.CreateSymbolicLink(destination, source);
    }

    public void LinkDirectory(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Um symlink de pasta aparece como diretório: Directory.Delete remove só o link, não o alvo
        if (Directory.Exists(destination)) Directory.Delete(destination);
        Directory.CreateSymbolicLink(destination, source);
    }
}