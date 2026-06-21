using Microsoft.EntityFrameworkCore;
using TCMine_Data.Data;

namespace TCMine_Data.Providers;

/// <summary>
/// Contexto concreto para PostgreSQL. As migrations deste provider ficam em
/// <c>Data/Migrations/Postgres</c> (geradas com <c>--context PostgresAppDbContext</c>).
/// </summary>
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options);