using Microsoft.Data.Sqlite;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Cria no arranque as pastas que a configuração aponta.
///     Existe porque nem todo consumidor cria a própria pasta, e o pior deles é
///     o primeiro a ser usado: o SQLite abre o arquivo mas NÃO cria o diretório,
///     então uma pasta <c>data/</c> ausente derruba a aplicação antes de
///     qualquer outra coisa rodar — e a mensagem que ele devolve ("unable to
///     open database file") não diz que o problema é uma pasta.
///     Uma pasta que falta é o estado normal de uma instalação nova, de um
///     volume recém-montado, ou de quem apagou <c>data/</c> para começar do
///     zero. Nenhum desses casos merece erro.
/// </summary>
public static class StoragePaths
{
    /// <summary>
    ///     Garante todas as pastas antes de qualquer serviço ser resolvido.
    ///     Chamar cedo importa: depois que o DbContext é construído já é tarde.
    /// </summary>
    public static void EnsureCreated(IConfiguration configuration, IHostEnvironment environment)
    {
        List<string> pastas = [];

        if (SqliteDirectoryOf(configuration["Database:ConnectionString"]) is { } bancoDir)
            pastas.Add(bancoDir);

        // Blob store: ele também cria a própria pasta ao ser construído, mas
        // isso acontece na primeira resolução do serviço — tarde demais se algo
        // antes dele já tiver falhado.
        if (configuration["BlobStorage:RootPath"] is { Length: > 0 } blobs)
            pastas.Add(blobs);

        if (configuration["Instances:RootPath"] is { Length: > 0 } instancias)
        {
            pastas.Add(instancias);

            // Os snapshots ficam FORA da pasta da instância, que o
            // materializador reescreve a cada troca de versão.
            pastas.Add(Path.Combine(instancias, "backups"));
        }

        // Chaves de proteção de dados. Sem elas persistidas, toda sessão cai a
        // cada arranque e o que foi cifrado antes (chave do CurseForge, senha de
        // SMTP) deixa de ser legível.
        pastas.Add(Path.Combine(environment.ContentRootPath, "data", "keys"));

        foreach (var pasta in pastas)
            Criar(pasta, environment.ContentRootPath);
    }

    /// <summary>
    ///     Diretório do arquivo apontado por uma connection string do SQLite.
    ///     Nulo quando não há o que criar: string vazia, banco em memória, ou um
    ///     caminho que já é a raiz. Função separada porque é a única parte disto
    ///     que tem casos suficientes para errar.
    /// </summary>
    public static string? SqliteDirectoryOf(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        string? dataSource;
        try
        {
            dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        }
        catch (ArgumentException)
        {
            // Connection string de outro provider (ou malformada): a validação
            // de configuração já reclama disso, e não é aqui que se decide.
            return null;
        }

        if (string.IsNullOrWhiteSpace(dataSource))
            return null;

        // ":memory:" e o formato de URI compartilhada não têm arquivo em disco.
        if (dataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("file::memory:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var diretorio = Path.GetDirectoryName(dataSource);

        // Arquivo solto na pasta de trabalho: não há diretório a criar.
        return string.IsNullOrEmpty(diretorio) ? null : diretorio;
    }

    private static void Criar(string caminho, string contentRoot)
    {
        // Relativo é relativo à raiz do conteúdo, não ao diretório de trabalho:
        // um serviço do systemd ou um container podem iniciar o processo de
        // qualquer lugar, e aí "data/" apontaria para outro lugar a cada vez.
        var absoluto = Path.IsPathRooted(caminho)
            ? caminho
            : Path.GetFullPath(Path.Combine(contentRoot, caminho));

        if (Directory.Exists(absoluto))
            return;

        // Sem log aqui de propósito: isto roda antes de o logging do host
        // existir, e criar pasta que falta é o comportamento esperado, não um
        // evento. O que merece ser dito é a falha, logo abaixo.
        try
        {
            Directory.CreateDirectory(absoluto);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Deixa subir com a causa junto: sem permissão de escrita, insistir
            // adiante só produziria o mesmo erro mais longe da origem, e a
            // mensagem do SQLite não diria que o problema era a pasta.
            throw new InvalidOperationException(
                $"Não foi possível criar a pasta '{absoluto}', configurada para armazenamento. "
                + $"Verifique as permissões de escrita. Causa: {ex.Message}", ex);
        }
    }
}
