namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     O endereço do servidor que veio DENTRO do instalador.
///     O TCMine Server empacota o launcher no arranque com o próprio endereço
///     público num <c>server.json</c> ao lado do executável — então quem baixou
///     o instalador de um servidor já sabe a qual servidor pertence, e o
///     jogador não digita nada no primeiro uso.
///     Só uma SUGESTÃO: o pareamento continua passando pelas mesmas regras
///     (HTTPS, handshake, versão do protocolo) de um endereço digitado.
/// </summary>
public interface IBundledServerAddress
{
    /// <summary>O endereço embutido, ou nulo num launcher genérico (build local, script manual).</summary>
    string? Get();
}
