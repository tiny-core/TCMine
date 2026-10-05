namespace TCMine.Server.Infrastructure.Launcher;

/// <summary>Seção "LauncherUpdates".</summary>
public sealed class LauncherBundleOptions
{
    /// <summary>Raiz do feed (<c>{TCMINE_ROOT}/updates/launcher</c> por padrão), com uma pasta por canal.</summary>
    public string? RootPath { get; set; }

    /// <summary>
    ///     Onde a imagem guarda o launcher compilado (<c>app/</c>), o <c>vpk</c>
    ///     (<c>vpk/</c>) e o <c>VERSION</c>. Fora de uma imagem a pasta não
    ///     existe, e a publicação embutida simplesmente não acontece.
    ///     Fora de <c>/opt/tcmine</c> de propósito: é onde o DEPLOY.md monta a
    ///     raiz de dados, e o volume esconderia esta pasta.
    /// </summary>
    public string BundlePath { get; set; } = "/usr/lib/tcmine/launcher";

    /// <summary>
    ///     Desligável para quem prefere publicar o launcher à mão
    ///     (<c>scripts/release-launcher.ps1</c>) — por exemplo, para assiná-lo.
    /// </summary>
    public bool PublishBundled { get; set; } = true;
}
