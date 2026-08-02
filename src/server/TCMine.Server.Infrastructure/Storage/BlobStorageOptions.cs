namespace TCMine.Server.Infrastructure.Storage;

/// <summary>
///     Configuração do armazenamento em disco. Preenchida pelo appsettings na
///     seção "BlobStorage".
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    ///     Raiz do store. Em Docker, precisa ser um volume — se ficar na camada
    ///     do container, o mod baixado some no próximo deploy.
    /// </summary>
    public string RootPath { get; set; } = "/var/lib/tcmine/blobs";

    /// <summary>
    ///     Tamanho do buffer de cópia. 80 KB fica logo abaixo do limite de 85 KB
    ///     que jogaria o array no Large Object Heap, que é coletado com muito
    ///     menos frequência.
    /// </summary>
    public int CopyBufferSize { get; set; } = 81920;
}
