namespace TCMine.Launcher.UI.Abstractions;

/// <summary>
///     A janela do console do jogo, que o host abre ao lado da principal.
///     Janela própria, e não um painel na tela inicial: o log é o que se olha
///     com o jogo aberto, e a janela principal pode estar minimizada nessa hora.
///     A tela só pede para mostrar; criar a janela e o WebView dela é do host,
///     como tudo o que é do sistema (ver <see cref="IWindowChrome" />).
/// </summary>
public interface IConsoleWindow
{
    /// <summary>Mostra a janela, criando-a na primeira vez, e a traz para a frente.</summary>
    void Show();
}
