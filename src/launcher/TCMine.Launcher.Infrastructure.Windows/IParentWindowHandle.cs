namespace TCMine.Launcher.Infrastructure.Windows;

/// <summary>
///     A janela sobre a qual o broker do Windows desenha o seu diálogo.
///     Mora AQUI e não no Core: um handle de janela é conceito do sistema
///     operativo, e o Core não deve conhecer nenhum — a regra de portabilidade
///     protege exatamente isso. O host WPF implementa-a; quem a consome é o MSAL.
///     Sem um pai, o diálogo do broker abre atrás do launcher e parece que o
///     login travou.
/// </summary>
public interface IParentWindowHandle
{
    /// <summary>
    ///     O HWND, ou zero quando ainda não há janela.
    ///     Zero é resposta válida: no arranque o login silencioso corre antes de
    ///     a janela existir, e ele não desenha nada.
    /// </summary>
    nint Handle { get; }
}
