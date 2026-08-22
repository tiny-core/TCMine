namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Confere se a pasta de instâncias está no MESMO caminho dentro e fora do
///     container.
///     Isto existe por causa de um erro que não dá sintoma. O TCMine pede ao
///     daemon do Docker que monte <c>{raiz}/{id}</c> no container do jogo, e
///     quem resolve esse caminho é o daemon, que enxerga o host. Se o bind
///     mount levou <c>/media/disco/tcmine</c> (host) para <c>/DATA/tcmine</c>
///     (container) — o que interfaces de NAS fazem sozinhas —, o daemon procura
///     <c>/DATA/tcmine/instances/{id}</c> no host, não acha, cria uma pasta
///     vazia e a monta. O servidor de jogo sobe sem mods e sem mundo, o painel
///     diz que está tudo certo, e ninguém liga isso a uma configuração de volume.
///     Recusar o arranque é desproporcional? Não: o custo de errar é descobrir
///     dias depois, com jogadores dentro de um mundo que não é o deles.
///     LIMITE CONHECIDO: o mountinfo de dentro do container diz o caminho da
///     origem RELATIVO ao sistema de arquivos dela, não onde esse sistema está
///     montado no host. Então "/AppData/x" servindo "/DATA/AppData/x" pode ser
///     coerente (se o disco estiver em /DATA no host) ou não, e a checagem
///     deixa passar. O que ela pega com certeza é a divergência de nome — que é
///     o caso que as interfaces de NAS produzem, renomeando a pasta no caminho.
/// </summary>
public static class MountCoherence
{
    /// <summary>Escape para o caso de a heurística errar num arranjo exótico.</summary>
    public const string SkipKey = "Storage:SkipMountCheck";

    public static void Verify(IConfiguration configuration)
    {
        if (configuration.GetValue(SkipKey, false))
            return;

        // Fora de container não há dois lados para divergir.
        if (!File.Exists("/proc/self/mountinfo"))
            return;

        if (configuration["Instances:RootPath"] is not { Length: > 0 } instancias)
            return;

        string[] linhas;
        try
        {
            linhas = File.ReadAllLines("/proc/self/mountinfo");
        }
        catch (IOException)
        {
            // Sem conseguir ler, não há o que afirmar.
            return;
        }

        if (Analisar(linhas, instancias) is { } problema)
            throw new InvalidOperationException(problema);
    }

    /// <summary>
    ///     Devolve a mensagem do problema, ou nulo quando está coerente.
    ///     Separado da leitura do arquivo para poder ser testado com um
    ///     mountinfo escrito à mão — que é o único jeito de exercitar isto sem
    ///     um container de verdade.
    /// </summary>
    public static string? Analisar(IReadOnlyList<string> mountInfo, string instancesPath)
    {
        var caminho = instancesPath.TrimEnd('/');

        string? melhorPonto = null;
        string? melhorOrigem = null;

        foreach (var linha in mountInfo)
        {
            // Formato: id pai major:minor ORIGEM PONTO opções... - tipo fonte ...
            var campos = linha.Split(' ');
            if (campos.Length < 5)
                continue;

            var origem = campos[3];
            var ponto = campos[4].TrimEnd('/');

            if (ponto.Length is 0)
                ponto = "/";

            if (!Contem(ponto, caminho))
                continue;

            // O mount mais específico é quem manda: /a e /a/b podem existir
            // juntos, e quem monta /a/b/c é o segundo.
            if (melhorPonto is null || ponto.Length > melhorPonto.Length)
            {
                melhorPonto = ponto;
                melhorOrigem = origem;
            }
        }

        // Nenhum mount cobre o caminho a não ser a raiz do container: a pasta
        // vive na camada da imagem, some ao recriar o container e o daemon não
        // a enxerga.
        if (melhorPonto is null or "/" || melhorOrigem is null)
        {
            return $"A pasta de instâncias ('{instancesPath}') não está num volume montado do host. "
                   + "Ela precisa ser um bind mount, porque o Docker resolve esse caminho no host ao "
                   + "criar o container do servidor de jogo — sem isso o servidor sobe sem mods e sem "
                   + "mundo. Ver docs/DEPLOY.md.";
        }

        // O campo de origem do mountinfo é o caminho DENTRO do sistema de
        // arquivos de origem, não o caminho absoluto do host. Comparar por
        // sufixo é o que funciona nos dois arranjos: bind na raiz
        // (/opt/x -> /opt/x, origem /opt/x) e bind num disco montado
        // (/media/hd/x -> /media/hd/x, origem /x).
        if (melhorOrigem is "/" || melhorPonto.EndsWith(melhorOrigem, StringComparison.Ordinal))
            return null;

        return $"A pasta de instâncias aponta para caminhos diferentes dentro e fora do container: "
               + $"aqui ela é '{melhorPonto}', mas no host ela termina em '{melhorOrigem}'. "
               + "Os dois lados precisam ser o MESMO caminho, porque quem resolve o caminho da "
               + "instância é o daemon do Docker, que enxerga o host — com eles diferentes, cada "
               + "servidor de jogo sobe com uma pasta vazia e nada acusa o erro. "
               + $"Ajuste o bind mount para 'origem:origem' e aponte Instances:RootPath para lá. "
               + $"Se este aviso estiver errado no seu arranjo, desligue com {SkipKey}=true.";
    }

    /// <summary>O caminho está sob este ponto de montagem?</summary>
    private static bool Contem(string ponto, string caminho) =>
        ponto is "/"
        || caminho.Equals(ponto, StringComparison.Ordinal)
        || caminho.StartsWith(ponto + "/", StringComparison.Ordinal);
}
