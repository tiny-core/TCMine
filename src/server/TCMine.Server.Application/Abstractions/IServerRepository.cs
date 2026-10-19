using TCMine.Contracts.Hubs;
using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Abstractions;

public interface IServerRepository
{
    /// <summary>Todos os servidores, de todos os modpacks — usado pelo painel.</summary>
    Task<IReadOnlyList<GameServer>> ListAllAsync(CancellationToken ct);

    Task<IReadOnlyList<GameServer>> ListByModpackAsync(Guid modpackId, CancellationToken ct);
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct);
    [Invalidates(ResourceKind.Servers)]
    Task AddAsync(GameServer server, CancellationToken ct);
    /// <summary>
    ///     Grava o que o formulário do servidor edita (nome, endereço, porta,
    ///     memória, opções…). NÃO grava as colunas que têm escritor próprio —
    ///     status, container, versão fixada, nuvem e marca de mundo —, mesmo que a
    ///     cópia passada as traga diferentes: quem chama leu o servidor antes, e
    ///     a cópia dele pode estar atrás do que esses escritores gravaram.
    /// </summary>
    [Invalidates(ResourceKind.Servers)]
    Task UpdateAsync(GameServer server, CancellationToken ct);

    /// <summary>Grava SÓ o container do servidor, depois de o orquestrador criá-lo.</summary>
    [NotVisibleToLauncher("O launcher não sabe qual container roda o servidor.")]
    Task SetContainerIdAsync(Guid id, string? containerId, CancellationToken ct);

    /// <summary>Grava SÓ a versão fixada no servidor (a troca de versão, depois do backup).</summary>
    [Invalidates(ResourceKind.Servers)]
    Task SetPinnedVersionAsync(Guid id, Guid modpackVersionId, CancellationToken ct);

    /// <summary>
    ///     Grava SÓ a nuvem ligada ao servidor (nula = desligado). A regra de quem
    ///     pode ligar a quê fica na entidade (<c>AttachToCloudVault</c>); isto só
    ///     persiste o resultado.
    /// </summary>
    [NotVisibleToLauncher("O launcher não vê a nuvem do servidor; o mod a lê pela chave.")]
    Task SetCloudVaultAsync(Guid id, Guid? cloudVaultId, CancellationToken ct);

    /// <summary>
    ///     Grava SÓ o status. Existe para quem acompanha o Docker: ele lê o
    ///     servidor e grava um instante depois, e gravar a linha inteira desfaria
    ///     o que outra pessoa salvou nesse meio tempo.
    /// </summary>
    [Invalidates(ResourceKind.Servers)]
    Task UpdateStatusAsync(Guid id, GameServerStatus status, CancellationToken ct);

    /// <summary>
    ///     Registra que o servidor tem mundo, gravando SÓ essa coluna e só se
    ///     ela ainda estiver vazia. Verdadeiro quando gravou agora.
    ///     O "só se vazia" fica na própria consulta, não num if de quem chama:
    ///     o acompanhamento do Docker e o preenchimento do arranque podem chegar
    ///     ao mesmo servidor ao mesmo tempo, e a primeira data é a que vale.
    /// </summary>
    [NotVisibleToLauncher("O launcher não sabe se o servidor tem mundo; só o painel usa.")]
    Task<bool> MarkWorldInitializedAsync(Guid id, DateTimeOffset at, CancellationToken ct);
    [Invalidates(ResourceKind.Servers)]
    Task RemoveAsync(Guid id, CancellationToken ct);

    /// <summary>Snapshots de um servidor, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<WorldBackup>> ListBackupsAsync(Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Quantidade e bytes totais de TODOS os backups, de todos os
    ///     servidores — agregado no banco numa consulta só. A tela de storage só
    ///     quer o total; buscar backup por backup, um servidor de cada vez, seria
    ///     N consultas para somar um número que o próprio banco já sabe somar.
    /// </summary>
    Task<(int Count, long TotalBytes)> GetBackupUsageAsync(CancellationToken ct);

    Task<WorldBackup?> GetBackupAsync(Guid backupId, CancellationToken ct);

    [NotVisibleToLauncher("Backups de mundo só aparecem no painel.")]
    Task AddBackupAsync(WorldBackup backup, CancellationToken ct);

    [NotVisibleToLauncher("Backups de mundo só aparecem no painel.")]
    Task RemoveBackupAsync(Guid backupId, CancellationToken ct);
}
