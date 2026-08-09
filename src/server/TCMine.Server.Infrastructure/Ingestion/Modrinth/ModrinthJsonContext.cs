using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

// Source generator, como no Contracts: sem reflection, e o trimmer não remove
// os tipos por engano.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<ModrinthVersion>))]
[JsonSerializable(typeof(ModrinthProject))]
[JsonSerializable(typeof(ModrinthPackIndex))]
[JsonSerializable(typeof(ModrinthPackVersion))]
[JsonSerializable(typeof(IReadOnlyList<ModrinthPackVersion>))]
[JsonSerializable(typeof(ModrinthPackSearchResponse))]
internal sealed partial class ModrinthJsonContext : JsonSerializerContext;
