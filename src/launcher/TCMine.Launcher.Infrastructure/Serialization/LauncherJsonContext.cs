using System.Text.Json;
using System.Text.Json.Serialization;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Infrastructure.Serialization;

/// <summary>
///     Serialização dos arquivos que só o launcher escreve.
///     Separado do <c>TcMineJsonContext</c> porque aquele vive em
///     <c>TCMine.Contracts</c>, que por regra não depende de projeto nenhum — e o
///     manifesto de instância é um tipo do launcher, não do protocolo.
///     Repare no que NÃO existe aqui: um campo estático inicializado com
///     <c>TypeInfoResolver = Default</c>. Esse padrão cria um ciclo de
///     inicialização estática que deixa o contexto padrão sem resolver, e todo
///     <c>Default.QualquerCoisa</c> passa a estourar em runtime. Ver o §8 do
///     CLAUDE.md.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(InstanceManifest))]
public sealed partial class LauncherJsonContext : JsonSerializerContext
{
    private static readonly Lazy<JsonSerializerOptions> Lazy = new(() =>
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = Default });

    public static new JsonSerializerOptions Options => Lazy.Value;
}
