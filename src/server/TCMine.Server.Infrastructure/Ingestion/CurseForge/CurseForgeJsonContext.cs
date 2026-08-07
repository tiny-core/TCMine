using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

// Source generator, como no Modrinth: sem reflection e a salvo do trimmer.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CurseForgeResponse<CurseForgeMod>))]
[JsonSerializable(typeof(CurseForgeResponse<IReadOnlyList<CurseForgeMod>>))]
[JsonSerializable(typeof(CurseForgeResponse<IReadOnlyList<CurseForgeFile>>))]
internal sealed partial class CurseForgeJsonContext : JsonSerializerContext;
