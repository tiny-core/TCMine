using System.Text.Json;

namespace TCMine.Server.Infrastructure.Docker;

/// <summary>
///     Transforma a recusa do Docker em algo que o admin consegue ler.
///     O daemon responde o motivo no corpo (<c>{"message":"..."}</c>), mas o
///     <c>EnsureSuccessStatusCode</c> jogava isso fora e sobrava "Response
///     status code does not indicate success: 500" — o painel dizia QUE falhou
///     e escondia POR QUÊ. Os casos que o admin resolve sozinho (porta ocupada,
///     disco cheio, imagem que não existe) ganham uma frase em português; o
///     resto passa com o texto do próprio Docker, que ainda é melhor que um
///     número.
/// </summary>
public static class DockerError
{
    /// <summary>
    ///     Trecho da mensagem do Docker → o que dizer. A ordem importa: a
    ///     primeira que casar vence, e "permission denied" aparece dentro de
    ///     mensagens mais específicas.
    /// </summary>
    private static readonly (string Fragment, string Explanation)[] Known =
    [
        ("port is already allocated",
            "a porta já está em uso por outro container. Escolha outra porta para o servidor ou pare quem a ocupa."),
        ("address already in use",
            "a porta já está em uso por outro programa nesta máquina. Escolha outra porta para o servidor ou feche quem a ocupa."),
        ("no space left on device",
            "o disco está cheio. Libere espaço (backups e imagens antigas costumam ser os maiores) e tente de novo."),
        ("manifest unknown",
            "essa imagem não existe no registro. Confira a versão do Minecraft do modpack."),
        ("manifest for",
            "essa imagem não existe no registro. Confira a versão do Minecraft do modpack."),
        ("pull access denied",
            "o registro recusou o download da imagem."),
        ("no such image",
            "a imagem do servidor não está nesta máquina e não foi possível baixá-la. Confira a conexão com a internet."),
        ("toomanyrequests",
            "o Docker Hub limitou os downloads desta máquina. Espere alguns minutos e tente de novo."),
        ("i/o timeout",
            "o registro de imagens não respondeu. Confira a conexão com a internet."),
        ("no such host",
            "não foi possível resolver o endereço do registro de imagens. Confira a conexão e o DNS desta máquina."),
        ("is not shared from the host",
            "a pasta de dados não está compartilhada com o Docker. No Docker Desktop, libere-a em Settings → Resources → File sharing."),
        ("mounts denied",
            "o Docker não tem acesso à pasta de dados. No Docker Desktop, libere-a em Settings → Resources → File sharing."),
        ("permission denied",
            "o Docker recusou por falta de permissão. Confira o dono da pasta de dados e o acesso ao socket do Docker."),
        ("minimum memory limit",
            "a memória pedida é menor que o mínimo que o Docker aceita."),
        ("is already in use by container",
            "já existe um container com este nome. Tente de novo; se persistir, remova-o no Docker.")
    ];

    /// <summary>
    ///     Monta a frase. <paramref name="action" /> completa "Docker não
    ///     conseguiu …" (por exemplo "iniciar o container").
    /// </summary>
    public static string Describe(string action, string? detail)
    {
        var raw = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();

        if (raw is null)
            return $"Docker não conseguiu {action}, e não disse por quê.";

        foreach (var (fragment, explanation) in Known)
        {
            // O texto original vai junto: é o que se cola numa busca ou num
            // pedido de ajuda quando a explicação não bastar.
            if (raw.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return $"Docker não conseguiu {action}: {explanation} (Docker: {raw})";
        }

        return $"Docker não conseguiu {action}: {raw}";
    }

    /// <summary>
    ///     Tira a mensagem de um corpo de erro do daemon. Aceita os dois formatos
    ///     que ele usa — <c>{"message":"..."}</c> nas rotas comuns e
    ///     <c>{"error":"..."}</c> nas linhas de progresso do pull — e devolve o
    ///     próprio texto quando não é JSON (um proxy no caminho, por exemplo).
    ///     Nulo quando não há erro nenhum ali.
    /// </summary>
    public static string? Extract(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind is not JsonValueKind.Object)
                return null;

            foreach (var name in MessageFields)
            {
                if (doc.RootElement.TryGetProperty(name, out var value)
                    && value.ValueKind is JsonValueKind.String
                    && value.GetString() is { Length: > 0 } text)
                    return text;
            }

            return null;
        }
        catch (JsonException)
        {
            return body.Trim();
        }
    }

    private static readonly string[] MessageFields = ["message", "error"];
}
