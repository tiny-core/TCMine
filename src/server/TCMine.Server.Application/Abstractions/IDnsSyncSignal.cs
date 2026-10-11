namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Avisa que o DNS dos servidores pode ter ficado desatualizado.
///     É um aviso, não a sincronização: quem cria, edita ou apaga um servidor
///     não espera a Cloudflare responder nem falha por causa dela. O pedido é
///     atendido logo a seguir, em segundo plano, e vários pedidos seguidos viram
///     uma sincronização só.
///     Chamar sem o DNS configurado não custa nada — a sincronização confere a
///     configuração antes de sair para a rede.
/// </summary>
public interface IDnsSyncSignal
{
    void Request();
}
