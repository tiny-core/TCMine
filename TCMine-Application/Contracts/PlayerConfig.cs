namespace TCMine_Application.Contracts;

/// <summary>
///     Manifesto das configs player-owned de um <c>(uuid, modpackId)</c>: mapeia cada arquivo (caminho
///     relativo à pasta do jogo, com '/') ao seu hash e tamanho. É a base do <b>sync incremental</b>
///     ([[concepts/player-config-sync]]): comparando manifestos, só os arquivos que mudaram trafegam,
///     em vez do zip inteiro. O servidor guarda-o como <c>.tcmine-manifest.json</c> ao lado dos arquivos;
///     o launcher recalcula-o a partir do disco.
/// </summary>
public sealed record PlayerConfigManifest(
    DateTimeOffset UpdatedAt,
    Dictionary<string, PlayerConfigFileInfo> Files);

/// <summary>Hash (SHA-256, hex) e tamanho de um arquivo no manifesto.</summary>
public sealed record PlayerConfigFileInfo(string Hash, long Size);

/// <summary>Pedido de bundle: os caminhos (relativos) que o cliente quer baixar do servidor.</summary>
public sealed record PlayerConfigBundleRequest(List<string> Paths);