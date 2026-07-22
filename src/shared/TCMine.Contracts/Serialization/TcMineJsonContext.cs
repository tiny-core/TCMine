using System.Text.Json;
using System.Text.Json.Serialization;
using TCMine.Contracts.Handshake;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;

namespace TCMine.Contracts.Serialization;

/// <summary>
///     Source generator de serialização: o código de leitura e escrita de cada
///     tipo é gerado em tempo de compilação, sem reflection em runtime.
///     Ganha desempenho e, principalmente, compatibilidade com trimming — o
///     serializador por reflection quebra quando o trimmer remove os tipos.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LauncherConfig))]
[JsonSerializable(typeof(HandshakeResponse))]
[JsonSerializable(typeof(ModpackDto))]
[JsonSerializable(typeof(IReadOnlyList<ModpackDto>))]
[JsonSerializable(typeof(ModpackVersionDto))]
[JsonSerializable(typeof(GameServerDto))]
[JsonSerializable(typeof(IReadOnlyList<GameServerDto>))]
public sealed partial class TcMineJsonContext : JsonSerializerContext
{
    /// <summary>
    ///     Use estas options em qualquer lugar. Se server e launcher usarem
    ///     configurações diferentes, um escreve camelCase e o outro, espera
    ///     PascalCase — e o bug só aparece em produção.
    /// </summary>
    public new static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = Default
    };
}