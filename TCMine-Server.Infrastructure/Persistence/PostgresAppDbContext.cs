using Microsoft.EntityFrameworkCore;

namespace TCMine_Server.Infrastructure.Persistence;

/// <summary>
///     Contexto concreto para PostgresSQL. As migrations deste provider ficam em
///     <c>Data/Migrations/Postgres</c> (geradas com <c>--context PostgresAppDbContext</c>).
/// </summary>
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options);