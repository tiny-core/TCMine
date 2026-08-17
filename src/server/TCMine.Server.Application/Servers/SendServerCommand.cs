using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Security;

namespace TCMine.Server.Application.Servers;

/// <summary>
///     Executa um comando de console pedido pelo launcher.
///     O launcher NUNCA fala RCON: a senha não sai do servidor, e é aqui que o
///     pedido vira comando. Esta é a única porta pela qual um jogador alcança o
///     console do jogo, então tudo o que a protege mora nesta classe — papel,
///     allowlist e a forma do que é enviado.
/// </summary>
public sealed class SendServerCommand(
    IRconClient rcon,
    IServerOrchestrator orchestrator,
    ICurrentUserScope scope)
{
    /// <summary>
    ///     Um comando é uma palavra: letras, dígitos, hífen ou sublinhado.
    ///     Verificar a FORMA antes da allowlist fecha a brecha de mandar
    ///     "list algo" como se fosse o comando — a comparação com a allowlist é
    ///     por igualdade, então não passaria, mas depender disso deixaria a
    ///     segurança na mão do formato de uma lista que outra pessoa vai editar.
    /// </summary>
    private static bool FormaValida(string command) =>
        command.Length is > 0 and <= 32
        && command.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public async Task<Result<string>> HandleAsync(
        Guid serverId,
        string command,
        IReadOnlyList<string> args,
        CancellationToken ct)
    {
        var role = await scope.GetRoleAsync(serverId, ct);

        // Mesma mensagem de "não existe" para quem não tem vínculo: a lista de
        // servidores que existem não é informação pública.
        if (role is not { } papel)
            return Result<string>.Fail("Servidor não encontrado.");

        if (string.IsNullOrWhiteSpace(command) || !FormaValida(command))
            return Result<string>.Fail("Comando inválido.");

        // Não diz qual papel seria necessário: informação de autorização é
        // pista para quem está sondando o sistema.
        if (!ConsoleCommandPolicy.IsAllowed(papel, command))
            return Result<string>.Fail("Comando não permitido para o seu nível de acesso.");

        // Caractere de controle num argumento não tem uso legítimo — nome de
        // jogador e mensagem de chat não têm quebra de linha. O rcon-cli recebe
        // argv e não passa por shell, então não há injeção de shell a temer;
        // isto é a camada que impede um argumento de virar outra coisa dentro do
        // próprio comando.
        if (args.Any(a => a.Any(char.IsControl)))
            return Result<string>.Fail("Argumento inválido.");

        // Com o servidor parado o rcon-cli falha com um erro de conexão que não
        // diz nada a quem está na tela. Responder aqui é a diferença entre
        // "servidor está parado" e "não foi possível falar com o servidor".
        var status = await orchestrator.GetStatusAsync(serverId, ct);
        if (status is not GameServerStatus.Running)
            return Result<string>.Fail("O servidor precisa estar no ar para receber comandos.");

        var linha = args.Count is 0 ? command : $"{command} {string.Join(' ', args)}";

        try
        {
            var saida = await rcon.ExecuteAsync(serverId, linha, ct);
            return Result<string>.Success(saida);
        }
        catch (RconUnavailableException ex)
        {
            // Indisponibilidade não é erro do usuário: devolver como falha de
            // regra (e não deixar subir) evita derrubar a conexão do hub inteira
            // por causa de um comando.
            return Result<string>.Fail(ex.Message);
        }
    }
}
