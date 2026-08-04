using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.Modrinth;

// Source generator, como no Contracts: sem reflection, e o trimmer não remove
// os tipos por engano.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<ModrinthVersion>))]
[JsonSerializable(typeof(ModrinthProject))]
internal sealed partial class ModrinthJsonContext : JsonSerializerContext;
