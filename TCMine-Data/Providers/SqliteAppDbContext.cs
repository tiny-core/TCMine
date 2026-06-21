using Microsoft.EntityFrameworkCore;
using TCMine_Data.Data;

namespace TCMine_Data.Providers;

/// <summary>
/// Contexto concreto para SQLite. As migrations deste provider ficam em
/// <c>Data/Migrations/Sqlite</c> (geradas com <c>--context SqliteAppDbContext</c>).
/// </summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);