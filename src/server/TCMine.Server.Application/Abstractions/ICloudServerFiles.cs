namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O arquivo com que o mod tccloud encontra a nuvem num servidor gerenciado
///     pelo TCMine: <c>tccloud-server.json</c> na raiz da pasta da instância
///     (<c>/data</c> no container), com a URL e a chave.
///     Arquivo e não variável de ambiente: as variáveis de um container são
///     fixadas na criação, e o TCMine não recria containers — trocar a chave ou
///     ligar a nuvem num servidor existente exigiria derrubá-lo. O arquivo é
///     reescrito a cada start. Fica fora de <c>config/</c> (que vem do modpack) e
///     fora do mundo (que vai para os backups).
/// </summary>
public interface ICloudServerFiles
{
    Task WriteAsync(Guid gameServerId, Uri cloudUrl, string key, CancellationToken ct);

    /// <summary>Idempotente: silêncio se o arquivo não existe.</summary>
    Task DeleteAsync(Guid gameServerId, CancellationToken ct);
}

/// <summary>
///     Endereço da API da nuvem visto DE DENTRO dos containers de jogo.
///     <c>Server:CloudUrl</c> quando o admin define (ex.: rede interna do
///     Docker); senão o <c>Server:PublicUrl</c>. Nulo = não configurado: a nuvem
///     não é entregue a nenhum servidor gerenciado.
/// </summary>
public interface ICloudEndpointSource
{
    Uri? ServerFacingUrl { get; }
}
