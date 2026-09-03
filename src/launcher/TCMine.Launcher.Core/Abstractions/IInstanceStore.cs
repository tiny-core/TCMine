using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     A pasta de cada instância no disco do jogador.
///     Toda escrita e toda remoção passam por aqui, e o caso de uso nunca toca
///     em <c>System.IO</c> — é o que permite testar a instalação inteira sem
///     criar arquivo nenhum.
/// </summary>
public interface IInstanceStore
{
    /// <summary>Caminho absoluto da pasta. Usado para abrir no explorador.</summary>
    string PathFor(InstanceKey key);

    Task<InstanceManifest?> ReadManifestAsync(InstanceKey key, CancellationToken ct);

    Task WriteManifestAsync(InstanceKey key, InstanceManifest manifest, CancellationToken ct);

    /// <summary>Tudo que está instalado nesta máquina, para a tela de instâncias.</summary>
    Task<IReadOnlyList<InstalledInstance>> ListAsync(CancellationToken ct);

    /// <summary>
    ///     Apaga arquivos GERENCIADOS que sobraram da versão anterior. Recebe
    ///     caminhos relativos e nunca decide sozinho o que remover: quem decide é
    ///     o diff, a partir do manifesto local.
    /// </summary>
    Task DeleteFilesAsync(InstanceKey key, IEnumerable<string> relativePaths, CancellationToken ct);

    /// <summary>Remove a instância inteira, mundo do jogador incluído.</summary>
    Task RemoveAsync(InstanceKey key, CancellationToken ct);
}

/// <summary>
///     Uma instância instalada, com a chave para agir sobre ela.
///     O caminho vem junto porque quem sabe onde a pasta está é o store, e a tela
///     precisa dele para abrir no explorador — recalculá-lo na interface
///     duplicaria a regra de nomeação de pasta em dois lugares.
/// </summary>
public sealed record InstalledInstance(
    InstanceKey Key,
    InstanceManifest Manifest,
    long SizeBytes,
    string Path);
