using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Core.Identity;

/// <summary>
///     O que dizer ao jogador quando o login com a Microsoft não deu certo.
///     Mora no Core, e não junto do MSAL, porque nada aqui é MSAL: são decisões
///     de produto — fechar a janela não é erro, precisar de interação não é
///     falha — e decisões de produto merecem teste. O que atravessa a fronteira é
///     o código de erro, que é uma <c>string</c>; a regra de arquitetura proíbe
///     depender do pacote, não conhecer o vocabulário dele.
///     A tradução é o que separa "não foi possível entrar" de uma instrução que o
///     jogador consegue seguir.
/// </summary>
public static class MicrosoftSignInFailures
{
    /// <summary>O jogador fechou o navegador ou cancelou no broker.</summary>
    public const string Cancelled = "authentication_canceled";

    /// <summary>A porta do loopback já estava ocupada por outro processo.</summary>
    public const string LoopbackOcupado = "http_listener_error";

    /// <summary>
    ///     O client id não existe, ou a app do Azure não tem a plataforma de
    ///     cliente público / o URI de redirecionamento configurados.
    /// </summary>
    public const string AppMalConfigurada = "invalid_client";

    /// <summary>
    ///     Traduz o código de erro do MSAL num desfecho para a tela.
    ///     O <paramref name="detalhe" /> é a mensagem original: entra no texto
    ///     apenas nos casos que ninguém previu, porque nesses ela é a única
    ///     pista — e some nos casos conhecidos, onde só atrapalharia.
    /// </summary>
    public static AuthResult Traduzir(string? code, string? detalhe) => code switch
    {
        // Uma decisão, não uma falha: avisar seria repetir ao jogador o que ele
        // acabou de fazer, e a tela trata este desfecho em silêncio.
        Cancelled => AuthResult.Cancelled(),

        LoopbackOcupado => AuthResult.Failed(
            "Não foi possível abrir o navegador para o login: outra aplicação está a ocupar a "
            + "porta que a Microsoft usa para responder. Feche outros launchers e tente de novo."),

        // O jogador não tem como resolver isto, e é importante que ele saiba —
        // senão tenta de novo indefinidamente contra uma app quebrada.
        AppMalConfigurada => AuthResult.Failed(
            "Este servidor não está corretamente registado na Microsoft. Avise o administrador: "
            + "o client ID nas configurações não corresponde a uma app de cliente público."),

        _ => AuthResult.Failed(
            string.IsNullOrWhiteSpace(detalhe)
                ? "Não foi possível entrar com a conta Microsoft."
                : $"Não foi possível entrar com a conta Microsoft: {detalhe}")
    };
}
