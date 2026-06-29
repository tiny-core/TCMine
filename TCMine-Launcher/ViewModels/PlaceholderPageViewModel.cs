namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Página "em breve" reutilizável: dá destino às abas cujo conteúdo ainda não foi implementado
/// (Jogar/Instâncias/Novidades/Definições), para a navegação do shell ficar completa. Uma só View
/// (<c>PlaceholderPageView</c>) serve a todas — instanciada com títulos/ícones diferentes.
/// </summary>
public sealed class PlaceholderPageViewModel(string title, string subtitle, string iconKey) : ViewModelBase
{
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;

    /// <summary>Chave do ícone em <c>Themes/Icons.axaml</c> (ex.: "IconInstances").</summary>
    public string IconKey { get; } = iconKey;
}
