namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O IP com que esta máquina aparece na internet.
///     É uma porta porque a resposta só existe fora do processo: quem sabe o IP
///     público é quem recebe a conexão, não quem a abre. A Application só precisa
///     de "qual é", e de nulo quando não deu para saber — é informação de
///     conveniência, e a falta dela nunca pode derrubar uma tela nem a lista de
///     servidores do launcher.
/// </summary>
public interface IPublicAddressProvider
{
    Task<string?> GetAsync(CancellationToken ct);
}
