using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Modpacks;

/// <summary>
///     Atualiza uma instância existente, com o mundo a salvo.
///     Atualizar é instalar por cima: o diff corre contra o manifesto que já está
///     lá, reescreve os mods e não toca no que é do jogador. O que este caso de
///     uso acrescenta é a rede de segurança — e a ordem, que é o essencial.
/// </summary>
public sealed class UpdateInstance(IInstanceInstaller install, IWorldBackup backup)
{
    public async Task<InstallResult> HandleAsync(
        Uri serverUrl,
        ModpackDto modpack,
        Guid versionId,
        InstalledInstance instance,
        bool backupWorld,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        // O backup ANTES, e a falha dele cancela a atualização. É a mesma regra
        // do lado do servidor, e é ela que torna a operação reversível: começar a
        // reescrever mods para só então descobrir que a cópia não foi feita
        // deixaria o jogador sem os dois caminhos — nem a versão antiga, nem a
        // garantia de poder voltar.
        if (backupWorld && backup.HasWorld(instance.Key))
        {
            progress?.Report(InstallProgress.BackingUp);

            try
            {
                await backup.CreateAsync(instance.Key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return InstallResult.Failure(
                    $"A atualização foi cancelada porque não foi possível copiar o mundo: {ex.Message}");
            }
        }

        return await install.HandleAsync(serverUrl, modpack, versionId, instance.Key, progress, ct);
    }
}

/// <summary>
///     O que o <see cref="UpdateInstance" /> precisa de um instalador.
///     Existe para esta orquestração poder ser verificada pelo que ela decide —
///     a ordem entre copiar e instalar, e em que instância instala — sem arrastar
///     uma instalação inteira para dentro do teste. A alternativa era deixar de
///     selar o <see cref="InstallModpackVersion" /> para o teste o poder herdar,
///     o que é furar o desenho para o teste ver.
/// </summary>
public interface IInstanceInstaller
{
    Task<InstallResult> HandleAsync(
        Uri serverUrl,
        ModpackDto modpack,
        Guid versionId,
        InstanceKey? target,
        IProgress<InstallProgress>? progress,
        CancellationToken ct);
}
