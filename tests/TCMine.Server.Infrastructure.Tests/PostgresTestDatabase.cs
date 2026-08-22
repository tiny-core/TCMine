using Microsoft.EntityFrameworkCore;
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
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "TCMINE_TEST_POSTGRES";

    private readonly string _database = $"tcmine_teste_{Guid.CreateVersion7():N}";
    private readonly string _servidor;

    private PostgresTestDatabase(string servidor) => _servidor = servidor;

    /// <summary>Connection string do servidor, ou nulo quando não há um configurado.</summary>
    public static string? ServerConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } valor ? valor : null;

    /// <summary>
    ///     Cria um banco só para este teste e aplica as migrations.
    ///     Um banco por teste, e não um compartilhado: migrations rodando em
    ///     paralelo sobre o mesmo banco produzem falhas que não têm nada a ver
    ///     com o que se está testando.
    /// </summary>
    public static async Task<PostgresTestDatabase> CreateAsync(CancellationToken ct)
    {
        var servidor = ServerConnectionString
                       ?? throw new InvalidOperationException(
                           $"{ConnectionVariable} não está definida. Use Assert.Skip antes de chamar.");

        var instancia = new PostgresTestDatabase(servidor);

        await using (var admin = new TcMineDbContext(Opcoes(servidor)))
        {
            // O nome do banco é gerado aqui a partir de um GUID, nunca vem de
            // fora — e CREATE DATABASE não aceita parâmetro, então interpolar é
            // o único caminho.
#pragma warning disable EF1002
            await admin.Database.ExecuteSqlRawAsync(
                $"CREATE DATABASE \"{instancia._database}\"", ct);
#pragma warning restore EF1002
        }

        await using var db = instancia.CreateContext();
        await db.Database.MigrateAsync(ct);

        return instancia;
    }

    public TcMineDbContext CreateContext() => new(Opcoes(ConnectionStringDoBanco()));

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

    private string ConnectionStringDoBanco() =>
        new NpgsqlConnectionStringBuilder(_servidor) { Database = _database }.ConnectionString;

    private static DbContextOptions<TcMineDbContext> Opcoes(string connectionString) =>
        new DbContextOptionsBuilder<TcMineDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly("TCMine.Server.Infrastructure.Postgres"))
            .Options;
}
