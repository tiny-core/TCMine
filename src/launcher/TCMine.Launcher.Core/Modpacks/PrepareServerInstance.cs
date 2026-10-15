using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Deixa a instância DO SERVIDOR pronta: acha a dele, ou instala uma nova, e
///     a põe na versão pinada nele.
///     A versão é pinada NO SERVIDOR, não no modpack (CLAUDE.md §5), e a
///     instância de servidor existe para segui-la: para cima ou para baixo, sem
///     backup. O mundo de quem joga num servidor mora no servidor; a pasta local
///     só leva mods e configs, e descer de versão nela não parte nada — o
///     contrário da instância pessoal (§7.0), que só anda para a frente e nunca
///     entra em servidor. Mundo próprio em <c>saves/</c> é exceção (alguém abriu
///     um mundo solo nela): segue, com aviso.
///     Uma instância por servidor, achada pelo id dele no manifesto. O armazém de
///     conteúdo partilhado faz o segundo download do mesmo pack ser só
///     materialização.
///     Caso de uso à parte do <see cref="JoinServer" /> porque a aba Servidores
///     também instala sem abrir o jogo — e a regra de achar, criar e alinhar tem
///     de ser uma só, senão "Instalar" e "Jogar" acabariam a discordar sobre qual
///     é a pasta do servidor.
/// </summary>
public sealed class PrepareServerInstance(
    IServerConnection connection,
    IInstanceStore instances,
    IInstanceInstaller installer,
    IWorldBackup worlds)
{
    /// <summary>
    ///     <paramref name="modpack" /> é o do servidor, vindo do catálogo que a
    ///     tela já tem: é o que cria a instância quando ainda não há nenhuma, e
    ///     pedi-lo ao servidor de novo seria uma ida à rede por dado em memória.
    /// </summary>
    public async Task<ServerInstanceResult> HandleAsync(
        Uri serverUrl,
        ModpackDto modpack,
        GameServerDto server,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        // Sem acesso a tela nem oferece instalar; a recusa aqui é para o caso
        // de o acesso ter caído com a tela aberta. Uma pasta para um servidor
        // em que não se entra é só disco gasto.
        if (server.AccessState is not ServerAccessState.Granted)
            return ServerInstanceResult.Failed("Você ainda não tem acesso a este servidor.");

        if (modpack.Id != server.ModpackId)
            return ServerInstanceResult.Failed($"{server.Name} roda outro modpack.");

        var key = await instances.FindServerInstanceAsync(server.Id, ct);
        var manifest = key is { } found ? await instances.ReadManifestAsync(found, ct) : null;

        // O admin trocou o pack do servidor. A pasta antiga não serve de base
        // (outro Minecraft, outro loader), mas pode ter um mundo solo: vira
        // pessoal em vez de ser apagada, e o servidor ganha uma instância nova.
        if (key is { } previous && manifest is not null && manifest.ModpackId != server.ModpackId)
        {
            await instances.WriteManifestAsync(previous, manifest with { ServerId = null, ServerName = null }, ct);
            key = null;
            manifest = null;
        }

        string? warning = null;

        if (manifest is null || manifest.ModpackVersionId != server.ModpackVersionId)
        {
            progress?.Report(InstallProgress.Planning);

            ModpackVersionDto target;

            try
            {
                target = await connection.GetModpackVersionAsync(server.ModpackVersionId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ServerInstanceResult.Failed(
                    $"Não foi possível consultar a versão de {server.Name}: {ex.Message}");
            }

            if (key is { } existing && manifest is not null
                && JoinServer.Decide(manifest.Version, target.Version) is JoinStep.Downgrade
                && worlds.HasWorld(existing))
            {
                warning = $"{server.Name} voltou para a v{target.Version}. O mundo solo guardado nesta "
                          + "instância (pasta saves) pode não abrir nesta versão — o do servidor não é afetado.";
            }

            var install = await installer.HandleAsync(serverUrl, modpack, target.Id, key, progress, ct);

            if (!install.Succeeded)
                return ServerInstanceResult.Failed(install.Error);

            key = install.Key!.Value;
            manifest = install.Instance!;
        }

        // A marca de servidor vai logo após a instalação, que grava o manifesto
        // sem ela numa instância nova. O nome acompanha um servidor renomeado no
        // painel: é o que a tela de instâncias mostra.
        var instanceKey = key!.Value;

        if (manifest.ServerId != server.Id || manifest.ServerName != server.Name)
        {
            manifest = manifest with { ServerId = server.Id, ServerName = server.Name };
            await instances.WriteManifestAsync(instanceKey, manifest, ct);
        }

        // Tamanho zero: somar a pasta é varrer um mundo inteiro, e quem abre o
        // jogo não o mostra. A tela relê a instância quando precisa dele.
        return new ServerInstanceResult(
            new InstalledInstance(instanceKey, manifest, 0, instances.PathFor(instanceKey)), warning, null);
    }
}

/// <summary>
///     A instância pronta, ou o motivo de não estar. O aviso não impede nada —
///     é o que a tela tem de mostrar mesmo quando tudo correu bem.
/// </summary>
public sealed record ServerInstanceResult(InstalledInstance? Instance, string? Warning, string? Error)
{
    public bool Succeeded => Instance is not null;

    public static ServerInstanceResult Failed(string? error) =>
        new(null, null, error ?? "Falha desconhecida.");
}
