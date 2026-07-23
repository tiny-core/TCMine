using TCMine.Contracts.Modpacks;

namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     Compara o manifest com o estado atual do disco.
///     Função pura: nenhuma operação de I/O acontece aqui. Quem chama já leu o
///     disco e passa o resultado como parâmetro. É o que torna esta lógica —
///     a mais delicada do launcher — testável em milissegundos, sem criar
///     arquivo nenhum.
/// </summary>
public static class ManifestDiffer
{
    /// <param name="instance"></param>
    /// <param name="manifest"></param>
    /// <param name="localFiles">
    ///     Caminho relativo → SHA-256 do que está na pasta da instância.
    /// </param>
    /// <param name="storeHashes">
    ///     Hashes já presentes no content store, de qualquer instância.
    /// </param>
    /// <param name="includeOptional">
    ///     Se o jogador optou por baixar os arquivos opcionais (shaders etc).
    /// </param>
    public static SyncPlan Plan(
        InstanceKey instance,
        ModpackVersionDto manifest,
        IReadOnlyDictionary<string, string> localFiles,
        IReadOnlySet<string> storeHashes,
        bool includeOptional)
    {
        // O mesmo mrpack serve os dois lados; aqui filtramos o que é do
        // cliente. Baixar um mod server-only não daria erro visível, mas
        // custaria banda e disco à toa.
        var desejados = manifest.Files
            .Where(f => f.Side is not FileSide.ServerOnly)
            .Where(f => includeOptional || !f.Optional)
            .ToList();

        var baixar = new List<ModpackFileDto>();
        var materializar = new List<ModpackFileDto>();

        foreach (var arquivo in desejados)
        {
            var jaEstaCorreto =
                localFiles.TryGetValue(arquivo.Path, out var hashLocal) &&
                string.Equals(hashLocal, arquivo.Sha256, StringComparison.OrdinalIgnoreCase);

            if (jaEstaCorreto)
                continue;

            materializar.Add(arquivo);

            // Só entra na fila de download se o conteúdo não existir em
            // lugar nenhum. Um mod compartilhado com outro modpack já
            // instalado não é baixado de novo.
            if (!storeHashes.Contains(arquivo.Sha256))
                baixar.Add(arquivo);
        }

        // Sobras da versão anterior. Sem esta limpeza, um mod removido do
        // pack continuaria carregando e travaria provavelmente o jogo.
        var caminhosDesejados = desejados
            .Select(f => f.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apagar = localFiles.Keys
            .Where(caminho => !caminhosDesejados.Contains(caminho))
            .ToList();

        return new SyncPlan
        {
            Instance = instance,
            ToDownload = baixar,
            ToMaterialize = materializar,
            ToDelete = apagar
        };
    }
}