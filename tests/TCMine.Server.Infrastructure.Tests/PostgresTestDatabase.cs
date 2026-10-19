using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Banco PostgreSQL de verdade para os testes que o SQLite não consegue
///     exercer.
///     Existe porque o SQLite é permissivo justamente onde o PostgreSQL não é:
///     ele aceita qualquer texto numa coluna <c>varchar(n)</c>, ignorando o
///     limite declarado. Foi assim que uma coluna curta demais passou por toda
///     a suíte e só apareceu ao importar um pack real em produção.
///     Sem a variável de ambiente, os testes que dependem disto são PULADOS, e
///     não falhados: quem roda a suíte na própria máquina não deve precisar
///     subir um banco para ver o resto passar. No CI a variável está definida, e
///     lá eles rodam de verdade.
///     É também a fábrica que os repositórios pedem: todo contexto vai ao banco
///     deste teste. Antes, cada classe de teste tinha a sua cópia dessa ponte.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable, IDbContextFactory<TcMineDbContext>
{
    public const string ConnectionVariable = "TCMINE_TEST_POSTGRES";

    private readonly string _database = $"tcmine_teste_{Guid.CreateVersion7():N}";
    private readonly string _servidor;

    private PostgresTestDatabase(string server)
    {
        _servidor = server;
    }

    /// <summary>Connection string do servidor, ou nulo quando não há um configurado.</summary>
    public static string? ServerConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } valor ? valor : null;

    public async ValueTask DisposeAsync()
    {
        // Fecha as conexões abertas antes do DROP: o PostgreSQL recusa apagar um
        // banco que ainda tem sessão ligada, e o teste seguinte herdaria o lixo.
        NpgsqlConnection.ClearAllPools();

        try
        {
            await using var admin = new TcMineDbContext(Opcoes(_servidor));
#pragma warning disable EF1002
            await admin.Database.ExecuteSqlRawAsync(
                $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
#pragma warning restore EF1002
        }
        catch (Exception)
        {
            // Limpeza não reprova teste; o banco do CI morre com o job.
        }
    }

    /// <summary>
    ///     Cria um banco só para este teste e aplica as migrations — todas, ou
    ///     até <paramref name="upToMigration" /> (inclusive), para o teste que
    ///     semeia o estado de antes de uma migration de dados e confere o depois.
    ///     Um banco por teste, e não um compartilhado: migrations rodando em
    ///     paralelo sobre o mesmo banco produzem falhas que não têm nada a ver
    ///     com o que se está testando.
    /// </summary>
    public static async Task<PostgresTestDatabase> CreateAsync(CancellationToken ct, string? upToMigration = null)
    {
        var server = ServerConnectionString
                     ?? throw new InvalidOperationException(
                         $"{ConnectionVariable} não está definida. Use Assert.Skip antes de chamar.");

        var instance = new PostgresTestDatabase(server);

        await using (var admin = new TcMineDbContext(Opcoes(server)))
        {
            // O nome do banco é gerado aqui a partir de um GUID, nunca vem de
            // fora — e CREATE DATABASE não aceita parâmetro, então interpolar é
            // o único caminho.
#pragma warning disable EF1002
            await admin.Database.ExecuteSqlRawAsync(
                $"CREATE DATABASE \"{instance._database}\"", ct);
#pragma warning restore EF1002
        }

        await instance.MigrateAsync(upToMigration, ct);
        return instance;
    }

    /// <summary>Aplica as migrations até <paramref name="target" /> (nulo = todas).</summary>
    public async Task MigrateAsync(string? target, CancellationToken ct)
    {
        await using var db = CreateContext();
        await db.GetService<IMigrator>().MigrateAsync(target, ct);
    }

    public TcMineDbContext CreateContext() => new(Opcoes(ConnectionStringDoBanco()));

    /// <summary>
    ///     Grava as entidades, na ordem dada, só nas colunas que a tabela TEM
    ///     agora — para o teste que parou numa migration antiga e semeia o "antes"
    ///     de uma migration de dados.
    ///     O SaveChanges não serve aí: ele monta o INSERT pelo modelo ATUAL, e
    ///     qualquer coluna acrescentada depois (a da retenção, por exemplo)
    ///     derruba o teste com "column does not exist" — sem que a migration
    ///     testada tenha mudado nada. Aqui a lista de colunas vem do próprio banco
    ///     (information_schema), e os valores, do modelo, com a mesma conversão
    ///     (enum como texto) que o EF aplicaria.
    /// </summary>
    public async Task InsertAsOfSchemaAsync(IEnumerable<object> entities, CancellationToken ct)
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var columnsByTable = new Dictionary<string, HashSet<string>>();

        foreach (var entity in entities)
        {
            var entry = db.Entry(entity);
            var table = entry.Metadata.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entry.Metadata.GetSchema());
            if (!columnsByTable.TryGetValue(table, out var existing))
                columnsByTable[table] = existing = await ColumnsAsync(connection, table, ct);

            await using var command = connection.CreateCommand();
            var names = new List<string>();
            foreach (var property in entry.Metadata.GetProperties())
            {
                if (property.GetColumnName(store) is not { } column || !existing.Contains(column))
                    continue;

                var value = entry.Property(property.Name).CurrentValue;
                if (value is not null && property.GetTypeMapping().Converter is { } converter)
                    value = converter.ConvertToProvider(value);

                names.Add(column);
                command.Parameters.Add(new NpgsqlParameter($"p{names.Count}", value ?? DBNull.Value));
            }

            // Nomes de tabela e coluna saem do modelo e do information_schema,
            // nunca de fora; os valores vão por parâmetro.
#pragma warning disable CA2100
            command.CommandText =
                $"INSERT INTO \"{table}\" ({string.Join(", ", names.Select(n => $"\"{n}\""))}) " +
                $"VALUES ({string.Join(", ", names.Select((_, i) => $"@p{i + 1}"))})";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<HashSet<string>> ColumnsAsync(NpgsqlConnection connection, string table,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        command.Parameters.Add(new NpgsqlParameter("table", table));
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(0));
        return columns;
    }

    TcMineDbContext IDbContextFactory<TcMineDbContext>.CreateDbContext() => CreateContext();

    private string ConnectionStringDoBanco() =>
        new NpgsqlConnectionStringBuilder(_servidor) { Database = _database }.ConnectionString;

    private static DbContextOptions<TcMineDbContext> Opcoes(string connectionString) =>
        new DbContextOptionsBuilder<TcMineDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly("TCMine.Server.Infrastructure.Postgres"))
            .Options;
}
