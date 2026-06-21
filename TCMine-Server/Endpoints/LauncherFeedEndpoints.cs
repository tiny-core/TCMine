using TCMine_Infrastructure.Launcher;

namespace TCMine_Server.Endpoints;

/// <summary>
/// Atalho de primeira instalação do launcher. O feed Velopack completo (RELEASES, nupkg, Setup.exe)
/// é servido como arquivos estáticos em <c>/updates</c> — é o que o autoupdate do cliente consome.
/// Este endpoint só devolve o <c>Setup.exe</c> mais recente para quem ainda não tem o launcher.
/// </summary>
public static class LauncherFeedEndpoints
{
    public static void MapLauncherFeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/download", (LauncherFeedService feed) =>
        {
            var setup = feed.LatestSetupExe();
            return setup is null
                ? Results.NotFound("O launcher ainda não foi compilado pelo servidor.")
                : Results.File(setup.FullName, "application/octet-stream", setup.Name);
        });
    }
}