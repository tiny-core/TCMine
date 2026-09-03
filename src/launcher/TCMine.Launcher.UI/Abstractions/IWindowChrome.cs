namespace TCMine.Launcher.UI.Abstractions;

/// <summary>
///     O que a barra de título desenhada em Blazor precisa da janela real.
///     A moldura do Windows está desligada para a janela inteira ser a nossa —
///     mesma linguagem visual do painel, sem uma faixa cinza do sistema em cima
///     do tema escuro. O preço é que arrastar, minimizar e maximizar deixam de
///     ser de graça: são operações do gestor de janelas, e HTML não as tem.
///     Esta porta é a costura. A UI continua portável; quem sabe conversar com o
///     Windows é o host.
/// </summary>
public interface IWindowChrome
{
    bool IsMaximized { get; }

    /// <summary>
    ///     Disparado quando a janela é maximizada ou restaurada — inclusive pelo
    ///     próprio Windows (Win+seta, arrastar para o topo, duplo clique na
    ///     borda). Sem ouvir isto, o ícone do botão mentiria metade do tempo.
    /// </summary>
    event Action? StateChanged;

    /// <summary>
    ///     Entrega o arrasto ao Windows a partir de um clique na barra de título.
    ///     Deve ser chamado durante o mouse-down: quem move a janela daí em
    ///     diante é o sistema, e é isso que preserva o snap às bordas e o
    ///     arrastar-para-maximizar sem reimplementarmos nada.
    /// </summary>
    void BeginDrag();

    void Minimize();

    void ToggleMaximize();

    void Close();
}
