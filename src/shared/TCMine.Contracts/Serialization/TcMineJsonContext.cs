using System.Text.Json;
using System.Text.Json.Serialization;
using TCMine.Contracts.Handshake;
using TCMine.Contracts.Identity;
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
[JsonSerializable(typeof(MinecraftLoginRequest))]
[JsonSerializable(typeof(LauncherSessionDto))]
[JsonSerializable(typeof(ModpackDto))]
[JsonSerializable(typeof(IReadOnlyList<ModpackDto>))]
[JsonSerializable(typeof(ModpackVersionDto))]
[JsonSerializable(typeof(GameServerDto))]
[JsonSerializable(typeof(IReadOnlyList<GameServerDto>))]
public sealed partial class TcMineJsonContext : JsonSerializerContext
{
    private static readonly Lazy<JsonSerializerOptions> Lazy = new(() =>
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = Default });

    /// <summary>
    ///     Use estas options em qualquer lugar. Se server e launcher usarem
    ///     configurações diferentes, um escreve camelCase e o outro, espera
    ///     PascalCase — e o bug só aparece em produção.
    ///     LAZY, e isto não é estilo. Como campo estático inicializado na
    ///     declaração, o inicializador lia <c>Default</c> durante a construção da
    ///     própria classe, ANTES de o gerador ter inicializado o campo de options
    ///     dele. O contexto padrão nascia sem TypeInfoResolver e ficava em cache
    ///     assim para sempre — e aí QUALQUER
    ///     <c>TcMineJsonContext.Default.QualquerCoisa</c> estourava com
    ///     "metadata ... was not provided by TypeInfoResolver of type
    ///     '&lt;null&gt;'". Adiar a construção para o primeiro uso quebra o ciclo.
    /// </summary>
    public static new JsonSerializerOptions Options => Lazy.Value;
}
