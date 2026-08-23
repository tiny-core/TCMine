using System.Text.Json.Serialization;

namespace TCMine.Server.Infrastructure.Ingestion.CurseForge;

// Source generator, como no Modrinth: sem reflection e a salvo do trimmer.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CurseForgeResponse<CurseForgeMod>))]
[JsonSerializable(typeof(CurseForgeResponse<IReadOnlyList<CurseForgeMod>>))]
[JsonSerializable(typeof(CurseForgeResponse<IReadOnlyList<CurseForgeFile>>))]
[JsonSerializable(typeof(CurseForgeManifest))]
[JsonSerializable(typeof(CurseForgeResponse<CurseForgeFile>))]
[JsonSerializable(typeof(CurseForgeModsRequest))]
[JsonSerializable(typeof(CurseForgeFilesRequest))]
internal sealed partial class CurseForgeJsonContext : JsonSerializerContext;
