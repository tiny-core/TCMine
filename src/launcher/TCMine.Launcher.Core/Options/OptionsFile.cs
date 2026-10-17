using System.Security.Cryptography;
using System.Text;

namespace TCMine.Launcher.Core.Options;

/// <summary>
///     O <c>options.txt</c> do Minecraft, lido e fundido linha a linha.
///     O formato é <c>nome:valor</c>, uma opção por linha. As teclas são as
///     linhas <c>key_*</c> (as do jogo e as de todo mod que usa o registro de
///     teclas dele, que é quase todos). Funções puras, e no Core por isso: errar
///     a fusão é apagar a configuração de alguém, e isso merece teste.
/// </summary>
public static class OptionsFile
{
    private const string KeyPrefix = "key_";

    /// <summary>
    ///     A versão de dados do arquivo. Viaja junto com as teclas, mas só entra
    ///     num arquivo que não tem a sua: sem ela, o jogo leria um options.txt
    ///     novo como se fosse de uma versão antiga e tentaria convertê-lo.
    /// </summary>
    private const string VersionName = "version";

    /// <summary>
    ///     O que vai para a conta. <paramref name="includeRest" /> falso deixa o
    ///     resto de fora: gráficos e som costumam ser do PC.
    /// </summary>
    public static OptionsPayload Extract(string text, bool includeRest)
    {
        var keys = new List<string>();
        var rest = new List<string>();

        foreach (var line in Lines(text))
        {
            var name = NameOf(line);

            if (name == VersionName || name?.StartsWith(KeyPrefix, StringComparison.Ordinal) is true)
                keys.Add(line);
            else
                rest.Add(line);
        }

        return new OptionsPayload(string.Join('\n', keys), includeRest ? string.Join('\n', rest) : null);
    }

    /// <summary>
    ///     Aplica o que veio da conta sobre o arquivo deste PC.
    ///     Opção que existe aqui é trocada no mesmo lugar; opção que não existe
    ///     entra no fim. Opção que só existe aqui FICA: é a tecla de um mod que
    ///     o outro PC ainda não conheceu, e apagá-la voltaria a tecla ao padrão.
    ///     A versão daqui sempre vence — quem a escreveu foi o jogo deste PC.
    /// </summary>
    /// <param name="rest">Nulo: só as teclas (sincronização completa desligada).</param>
    public static string Merge(string? local, string keyBindings, string? rest)
    {
        var lines = Lines(local ?? "").ToList();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Count; i++)
        {
            if (NameOf(lines[i]) is { } name)
                positions.TryAdd(name, i);
        }

        void Apply(string line)
        {
            if (NameOf(line) is not { } name)
                return;

            if (positions.TryGetValue(name, out var at))
            {
                if (name != VersionName)
                    lines[at] = line;

                return;
            }

            // Versão vai para o topo, que é onde o jogo a escreve.
            if (name == VersionName)
            {
                lines.Insert(0, line);
                foreach (var key in positions.Keys.ToList())
                    positions[key]++;
                positions[name] = 0;
                return;
            }

            positions[name] = lines.Count;
            lines.Add(line);
        }

        foreach (var line in Lines(keyBindings))
            Apply(line);

        if (rest is not null)
        {
            foreach (var line in Lines(rest))
                Apply(line);
        }

        return string.Join('\n', lines) + "\n";
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0);

    /// <summary>O nome antes dos dois-pontos, ou nulo numa linha que não segue o formato.</summary>
    private static string? NameOf(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? line[..colon] : null;
    }
}

/// <summary>As teclas (com a versão do arquivo) e, se pedido, o resto.</summary>
public sealed record OptionsPayload(string KeyBindings, string? Rest)
{
    /// <summary>
    ///     Impressão digital do conteúdo, para saber se mudou desde a última
    ///     sincronização sem guardar o arquivo inteiro.
    /// </summary>
    public string Fingerprint { get; } = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(KeyBindings + "\0" + (Rest ?? ""))));
}
