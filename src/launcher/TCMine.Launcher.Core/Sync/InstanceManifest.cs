using TCMine.Contracts.Modpacks;

namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     O registro do que o launcher escreveu numa instância.
///     É a fronteira entre o que é NOSSO e o que é DO JOGADOR, e existir é a
///     única razão pela qual um update pode apagar arquivos com segurança. Sem
///     ele, saber "o que sobrou da versão anterior" exigiria varrer a pasta — e
///     a varredura acharia <c>saves/</c>, <c>screenshots/</c> e
///     <c>options.txt</c>, que nunca estiveram no manifesto do pack e portanto
///     seriam apagados no primeiro update.
///     Mora dentro da pasta da instância, com nome pontuado para o jogador não
///     confundir com conteúdo do jogo.
/// </summary>
public sealed record InstanceManifest
{
    public const string FileName = ".tcmine-manifest.json";

    /// <summary>Versão do formato deste arquivo.</summary>
    public required int Schema { get; init; }

    public required Guid ModpackId { get; init; }

    public required Guid ModpackVersionId { get; init; }

    /// <summary>Nome do pack, para a tela de instâncias não depender do servidor.</summary>
    public required string ModpackName { get; init; }

    public required string Version { get; init; }

    public required DateTimeOffset InstalledAt { get; init; }

    /// <summary>
    ///     Caminho relativo → SHA-256 do que o launcher colocou aqui.
    ///     ESTE é o conjunto que vai ao <see cref="ManifestDiffer" />. Nada mais.
    /// </summary>
    public required IReadOnlyDictionary<string, string> ManagedFiles { get; init; }

    /// <summary>RAM escolhida pelo jogador. Nulo usa a recomendada do pack.</summary>
    public int? MemoryMb { get; init; }

    // ---------- O que é preciso para ABRIR o jogo ----------
    //
    // Gravado na instalação, e aqui — ao contrário do endereço de um servidor —
    // isso está certo: um Modpack fixa MinecraftVersion e Loader de forma
    // IMUTÁVEL, e a ModpackVersion fixa a LoaderVersion. Não há o que envelhecer.
    // É também o que permite jogar sem rede: sem estes campos, abrir o jogo
    // exigiria consultar o catálogo, e um pack instalado deixaria de funcionar
    // no avião.
    //
    // Nulos nos manifestos de Schema 1, escritos antes disto existir. Quem os
    // tiver precisa reinstalar a instância uma vez — e o caso de uso do launch
    // diz isso ao jogador em vez de adivinhar uma versão.

    public string? MinecraftVersion { get; init; }

    public ModLoader? Loader { get; init; }

    public string? LoaderVersion { get; init; }

    /// <summary>Tem o suficiente para abrir o jogo sem consultar o servidor.</summary>
    public bool CanLaunch => !string.IsNullOrWhiteSpace(MinecraftVersion) && Loader is not null;
}
