using System.Text.Json.Serialization;

namespace TCMine.Launcher.Infrastructure.Runtime;

/// <summary>
///     O pedaço da API do Adoptium que nos interessa.
///     Contexto próprio, separado do <c>LauncherJsonContext</c>, pela mesma razão
///     do Xbox: é formato de terceiro, não nosso, e a resposta traz dezenas de
///     campos que nunca vamos ler — declarar só estes documenta o acoplamento
///     real.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AdoptiumAsset[]))]
internal sealed partial class AdoptiumJsonContext : JsonSerializerContext;

internal sealed record AdoptiumAsset
{
    public AdoptiumBinary? Binary { get; init; }
}

internal sealed record AdoptiumBinary
{
    public AdoptiumPackage? Package { get; init; }
}

internal sealed record AdoptiumPackage
{
    /// <summary>URL do arquivo. Zip no Windows, tar.gz no resto.</summary>
    public string? Link { get; init; }

    /// <summary>SHA-256 em hexadecimal, como o Adoptium publica.</summary>
    public string? Checksum { get; init; }

    /// <summary>Nome do arquivo. É dele que sai a extensão que decide o extrator.</summary>
    public string? Name { get; init; }

    /// <summary>Tamanho em bytes, para o progresso não depender de Content-Length.</summary>
    public long Size { get; init; }
}
