namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     O servidor de e-mail da instalação, rodando como container.
///     Existe separado do <see cref="IServerOrchestrator" /> porque não é um
///     servidor de jogo: não tem mundo, não tem versão de modpack, e o ciclo de
///     vida dele é da instalação inteira, não de um recurso que o admin cria
///     várias vezes.
///     Só ENVIO. Receber e-mail exigiria caixas postais, antispam, certificado e
///     porta 25 aberta para a internet — e nada no TCMine lê mensagem: os
///     endereços que ele usa são de não-resposta.
/// </summary>
public interface IMailServerOrchestrator
{
    Task<MailServerState> GetStateAsync(CancellationToken ct);

    /// <summary>
    ///     Cria o container se preciso e o põe no ar, com o domínio informado.
    ///     Idempotente: chamar com o servidor já rodando não faz nada.
    /// </summary>
    Task StartAsync(string domain, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    /// <summary>
    ///     Remove o container. NÃO apaga a pasta de dados — a chave DKIM está lá
    ///     dentro, e regenerá-la obrigaria a republicar o DNS e derrubaria a
    ///     entrega até a propagação terminar.
    /// </summary>
    Task RemoveAsync(CancellationToken ct);

    /// <summary>
    ///     Valor pronto do registro DKIM, lido de dentro do container. Nulo
    ///     enquanto a chave não existir — ela é gerada no primeiro arranque.
    /// </summary>
    Task<string?> GetDkimRecordAsync(string domain, CancellationToken ct);

    /// <summary>
    ///     Cria (ou atualiza a senha da) conta de envio. É por ela que o
    ///     TCMine autentica no próprio servidor de e-mail.
    /// </summary>
    Task EnsureSenderAccountAsync(string address, string password, CancellationToken ct);
}

public enum MailServerState
{
    /// <summary>Nunca foi criado nesta instalação.</summary>
    NotCreated,

    Stopped,
    Starting,
    Running,
    Crashed
}
