namespace TCMine.Launcher.UI.Abstractions;

/// <summary>
///     O pouco que a interface precisa do ambiente de trabalho.
///     Abrir uma pasta é uma operação do sistema, e HTML não a tem. Mesma costura
///     do <see cref="IWindowChrome" />: a tela continua portável, e quem sabe
///     falar com o Windows é o host.
/// </summary>
public interface IDesktopShell
{
    /// <summary>Abre a pasta no explorador de arquivos.</summary>
    void OpenFolder(string path);

    /// <summary>
    ///     Abre um endereço no navegador padrão do sistema — nunca dentro do
    ///     WebView2 da janela, que navegaria embora da tela em vez de abrir
    ///     uma aba nova. Mesma regra do login da Microsoft (CLAUDE.md §7.1):
    ///     link externo é coisa do navegador do jogador, não desta janela.
    /// </summary>
    void OpenUrl(string url);
}
