using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCMine_Application.Abstractions;
using TCMine_Server.Infrastructure.Persistence.Repositories;

namespace TCMine_Server.Infrastructure.Persistence;

/// <summary>
/// Registro e inicialização da camada de dados. Concentra num só lugar a escolha
/// do provider (SQLite/Postgres) e o mapeamento da base abstrata
/// <see cref="AppDbContext"/> para a subclasse concreta — assim os serviços
/// dependem apenas de <see cref="AppDbContext"/> e ignoram o provider.
/// </summary>
public static partial class DatabaseServiceCollectionExtensions
{
    // Padrões por provider quando a connection string não é informada
    private const string DefaultSqliteConnection = "Data Source=data-server/tcmine.db";

    private const string DefaultPostgresConnection =
        "Host=localhost;Database=tcmine;Username=postgres;Password=postgres";

    /// <summary>
    /// Registra o <see cref="AppDbContext"/> no DI conforme o provider configurado.
    /// Prioridade de config: env vars DB_PROVIDER/DB_CONNECTION &gt; seção "Database" do
    /// appsettings &gt; padrão (SQLite). Env vars ganham para facilitar Docker/produção.
    /// </summary>
    public static IServiceCollection AddTcMineDatabase(
        this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                      ?? new DatabaseOptions();

        // Env vars têm prioridade — sobrepõem o que veio do appsettings
        if (Enum.TryParse<DatabaseProvider>(config["DB_PROVIDER"], true, out var envProvider))
            options.Provider = envProvider;

        var envConnection = config["DB_CONNECTION"];
        if (!string.IsNullOrWhiteSpace(envConnection))
            options.ConnectionString = envConnection;

        switch (options.Provider)
        {
            case DatabaseProvider.Postgres:
                var pgConnection = options.ConnectionString ?? DefaultPostgresConnection;
                services.AddDbContext<PostgresAppDbContext>(o => o.UseNpgsql(pgConnection));
                // Mapeia a base abstrata para a concreta — uma instância por escopo/requisição
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<PostgresAppDbContext>());
                break;

            case DatabaseProvider.Sqlite:
            default:
                var sqliteConnection = options.ConnectionString ?? DefaultSqliteConnection;
                services.AddDbContext<SqliteAppDbContext>(o => o.UseSqlite(sqliteConnection));
                services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteAppDbContext>());
                break;
        }

        // Repositórios (portas da Application → implementações EF). Scoped: usam o AppDbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IServerSettingsStore, ServerSettingsStore>();

        return services;
    }

    /// <summary>
    /// Aplica as migrations pendentes da camada de dados no Startup. Resolve o
    /// <see cref="AppDbContext"/> já registrado — o DI escolheu a subclasse concreta
    /// (SQLite/Postgres) conforme o provider configurado, então o caller não precisa
    /// repetir essa decisão. Cria um escopo temporário porque o <see cref="DbContext"/>
    /// é scoped e o aplicativo, no Startup, está fora de requisição.
    /// </summary>
    /// <param name="services">O <see cref="IServiceProvider"/> raiz da aplicação (ex.: <c>app.Services</c>).</param>
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