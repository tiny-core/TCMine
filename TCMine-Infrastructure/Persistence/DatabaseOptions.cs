namespace TCMine_Infrastructure.Persistence;

/// <summary>Provider de banco suportado pelo servidor.</summary>
public enum DatabaseProvider
{
    Sqlite,
    Postgres
}

/// <summary>
/// Configuração da camada de dados, lida da seção "Database" do appsettings
/// (ou das env vars DB_PROVIDER / DB_CONNECTION, que têm prioridade — ver
/// <see cref="DatabaseServiceCollectionExtensions"/>).
///
/// Esta config é <b>bootstrap</b>: precisa estar fora do banco, porque é ela que
/// diz como conectar ao banco. Tudo o mais (CF token, Azure client id, usuários)
/// passa a viver no próprio banco nas próximas etapas.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    // Provider ativo; SQLite é o padrão (zero-config para dev local)
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    // Connection string; se vazia, um padrão por provider é aplicado
    public string? ConnectionString { get; set; }
}