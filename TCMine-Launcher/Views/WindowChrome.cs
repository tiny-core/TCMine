using Avalonia.Controls;

namespace TCMine_Launcher.Views;

/// <summary>
///     Superfície de uma janela sem chrome nativo: a borda arredondada + fundo que envolve o conteúdo
///     (barra de título + corpo). Centraliza o "prefixo" de moldura repetido nas janelas. O conteúdo é o
///     <see cref="ContentControl.Content" />; o modelo está em <c>Themes/WindowChrome.axaml</c>.
/// </summary>
public class WindowChrome : ContentControl;