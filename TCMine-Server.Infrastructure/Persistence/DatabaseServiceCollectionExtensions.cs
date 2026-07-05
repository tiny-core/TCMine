using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TCMine_Application.Abstractions;
using TCMine_Server.Infrastructure.Persistence.Repositories;

namespace TCMine_Server.Infrastructure.Persistence;

/// <summary>
///     Registro e inicialização da camada de dados. Concentra num só lugar a escolha
///     do provider (SQLite/Postgres) e o mapeamento da base abstrata
///     <see cref="AppDbContext" /> para a subclasse concreta — assim os serviços
///     dependem apenas de <see cref="AppDbContext" /> e ignoram o provider.
/// </summary>
public static partial class DatabaseServiceCollectionExtensions
{
    // Padrões por provider quando a connection string não é informada
    private const string DefaultSqliteConnection = "Data Source=data-server/tcmine.db";

    private const string DefaultPostgresConnection =
        "Host=localhost;Database=tcmine;Username=postgres;Password=postgres";

    /// <summary>
    ///     Registra o <see cref="AppDbContext" /> no DI conforme o provider configurado.
    ///     Prioridade de config: env vars DB_PROVIDER/DB_CONNECTION &gt; seção "Database" do
    ///     appsettings &gt; padrão (SQLite). Env vars ganham para facilitar Docker/produção.
    /// </summary>
    public static IServiceCollection AddTcMineDatabase(
        this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                      ?? new DatabaseOptions();

        // Provider ativo: env DB_PROVIDER > seção Database do appsettings > padrão (Sqlite).
        if (Enum.TryParse<DatabaseProvider>(config["DB_PROVIDER"], true, out var envProvider))
            options.Provider = envProvider;

        // Connection string, por ordem de prioridade (env ganha para facilitar Docker):
        //  1. DB_CONNECTION — string completa; escape hatch, sobrepõe tudo.
        //  2. Vars separadas DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD — montadas aqui (só Postgres).
        //  3. Database:ConnectionString do appsettings.
        //  4. Padrão por provider.
        var connection =
            Trimmed(config["DB_CONNECTION"])
            ?? BuildConnectionFromParts(config, options.Provider)
            ?? Trimmed(options.ConnectionString)
            ?? DefaultConnectionFor(options.Provider);

        switch (options.Provider)
        {
            case DatabaseProvider.Postgres:
                services.AddDbContext<PostgresAppDbContext>(o => o.UseNpgsql(connection));
                // Mapeia a base abstrata para a concreta — uma instância por escopo/requisição
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<PostgresAppDbContext>());
                break;

            case DatabaseProvider.Sqlite:
            default:
                services.AddDbContext<SqliteAppDbContext>(o => o.UseSqlite(connection));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteAppDbContext>());
                break;
        }

        // Repositórios (portas da Application → implementações EF). Scoped: usam o AppDbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IServerSettingsStore, ServerSettingsStore>();

        return services;
    }

    /// <summary>Connection string padrão do provider quando nada é configurado.</summary>
    private static string DefaultConnectionFor(DatabaseProvider provider)
    {
        return provider == DatabaseProvider.Postgres ? DefaultPostgresConnection : DefaultSqliteConnection;
    }

    /// <summary>
    ///     Monta a connection string do Postgres a partir das env vars <b>separadas</b>
    ///     (<c>DB_HOST</c>, <c>DB_PORT</c>, <c>DB_NAME</c>, <c>DB_USER</c>, <c>DB_PASSWORD</c>). Devolve
    ///     <c>null</c> se o provider não é Postgres ou se nenhuma delas foi informada — aí o caller cai para o
    ///     appsettings/padrão. Usa <see cref="NpgsqlConnectionStringBuilder" /> para escapar valores com
    ///     caracteres especiais (ex.: senha com <c>;</c>). Partes ausentes recebem defaults sensatos.
    /// </summary>
    private static string? BuildConnectionFromParts(IConfiguration config, DatabaseProvider provider)
    {
        // Vars separadas são um conceito de Postgres (host/porta/user/senha); SQLite é só um ficheiro.
        if (provider != DatabaseProvider.Postgres) return null;

        var host = Trimmed(config["DB_HOST"]);
        var port = Trimmed(config["DB_PORT"]);
        var name = Trimmed(config["DB_NAME"]);
        var user = Trimmed(config["DB_USER"]);
        var password = Trimmed(config["DB_PASSWORD"]);

        // Nenhuma parte informada → deixa o appsettings/padrão decidir.
        if (host is null && port is null && name is null && user is null && password is null)
            return null;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host ?? "localhost",
            Database = name ?? "tcmine",
            Username = user ?? "postgres",
            Password = password
        };
        if (int.TryParse(port, out var parsedPort)) builder.Port = parsedPort;

        return builder.ConnectionString;
    }

    /// <summary>Normaliza para <c>null</c> quando vazio/espaços; senão devolve o valor sem espaços nas pontas.</summary>
    private static string? Trimmed(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    ///     Aplica as migrations pendentes da camada de dados no Startup. Resolve o
    ///     <see cref="AppDbContext" /> já registrado — o DI escolheu a subclasse concreta
    ///     (SQLite/Postgres) conforme o provider configurado, então o caller não precisa
    ///     repetir essa decisão. Cria um escopo temporário porque o <see cref="DbContext" />
    ///     é scoped e o aplicativo, no Startup, está fora de requisição.
    /// </summary>
    /// <param name="services">O <see cref="IServiceProvider" /> raiz da aplicação (ex.: <c>app.Services</c>).</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    public static async Task MigrateTcMineDatabaseAsync(
        this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Logger é opcional (best-effort): a ausência dele nunca pode impedir a migração.
        var contextName = db.GetType().Name;
        var logger = scope.ServiceProvider
            .GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(DatabaseServiceCollectionExtensions).FullName!);

        try
        {
            logger?.LogAAplicarMigrationsPendentesDeContext(contextName);
            await db.Database.MigrateAsync(ct);
            logger?.LogMigrationsDeContextAplicadasComSucesso(contextName);
        }
        catch (Exception ex)
        {
            logger?.LogFalhaAoAplicarMigrationsDeContext(contextName, ex);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Information, "A aplicar migrations pendentes de {Context}...")]
    static partial void LogAAplicarMigrationsPendentesDeContext(this ILogger logger, string context);

    [LoggerMessage(LogLevel.Information, "Migrations de {Context} aplicadas com sucesso.")]
    static partial void LogMigrationsDeContextAplicadasComSucesso(this ILogger logger, string context);

    [LoggerMessage(LogLevel.Error, "Falha ao aplicar migrations de {Context}.")]
    static partial void LogFalhaAoAplicarMigrationsDeContext(this ILogger logger, string context, Exception exception);
}