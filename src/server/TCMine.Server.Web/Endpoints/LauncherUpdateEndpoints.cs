using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace TCMine.Server.Web.Endpoints;

/// <summary>
///     O feed de atualização do launcher.
///     O handshake já prometia este endereço — <c>LauncherFeedUrl</c> aponta para
///     <c>/updates/launcher/{canal}/</c> desde sempre — e nada o servia. A
///     promessa passou a ter dente quando o protocolo subiu para 2: um launcher
///     antigo é recusado no handshake e mandado atualizar, e mandá-lo para uma
///     rota que não existe seria pior do que não o recusar.
///     Ficheiros estáticos, servidos de uma pasta que o administrador enche com
///     a saída do <c>vpk</c>. O TCMine não gera releases: quem os constrói é o
///     pipeline de build, e inventar aqui um formato próprio seria refazer o
///     Velopack pior.
/// </summary>
public static class LauncherUpdateEndpoints
{
    public const string RootKey = "LauncherUpdates:RootPath";

    public static IEndpointRouteBuilder MapLauncherUpdates(this IEndpointRouteBuilder app)
    {
        var raiz = app.ServiceProvider.GetRequiredService<IConfiguration>()[RootKey];

        // Sem pasta configurada não há feed, e isso é estado legítimo: uma
        // instalação que ainda não publica launcher nenhum. Registar a rota para
        // ela devolver 404 sempre seria prometer outra vez o que não existe.
        if (string.IsNullOrWhiteSpace(raiz))
            return app;

        Directory.CreateDirectory(raiz);

        app.MapGet("/updates/launcher/{channel}/{file}", (
                string channel,
                string file,
                CancellationToken ct) =>
            {
                if (!TentarResolver(raiz, channel, file, out var path))
                    return Results.NotFound();

                // O Velopack pede o RELEASES e depois o .nupkg. Nenhum dos dois
                // tem tipo registado, e sem um explícito o ASP.NET recusa-se a
                // servir — o launcher receberia 404 num ficheiro que existe.
                return Results.File(path, "application/octet-stream", enableRangeProcessing: true);
            })
            // Anónimo pela mesma razão do handshake: um launcher que PRECISA de
            // atualizar pode ser velho demais para saber autenticar-se. Os
            // ficheiros aqui são binários públicos de instalação, não dados de
            // ninguém.
            .AllowAnonymous()
            .WithName("LauncherUpdateFile");

        return app;
    }

    /// <summary>
    ///     O caminho do ficheiro dentro da pasta do canal, ou falso.
    ///     Confinamento explícito: <c>channel</c> e <c>file</c> vêm da URL, e um
    ///     <c>..</c> aqui serviria qualquer ficheiro que o processo consegue ler.
    ///     O ASP.NET normaliza muita coisa, mas "muita coisa" não é uma garantia
    ///     de que se dependa para servir disco.
    /// </summary>
    private static bool TentarResolver(string raiz, string channel, string file, out string path)
    {
        path = "";

        if (channel.Length is 0 || file.Length is 0)
            return false;

        var baseCompleta = Path.GetFullPath(raiz);
        var candidato = Path.GetFullPath(Path.Combine(baseCompleta, channel, file));

        if (!candidato.StartsWith(
                baseCompleta + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(candidato))
            return false;

        path = candidato;
        return true;
    }
}
