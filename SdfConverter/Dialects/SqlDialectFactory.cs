using System;

namespace SdfConverter.Dialects;

/// <summary>
/// Creates the <see cref="ISqlDialect"/> implementation for a target dialect.
/// </summary>
public static class SqlDialectFactory
{
    /// <summary>Returns the dialect implementation for the given kind.</summary>
    public static ISqlDialect Create(SqlDialectKind kind) => kind switch
    {
        SqlDialectKind.Postgres => new PostgresDialect(),
        SqlDialectKind.Sqlite => new SqliteDialect(),
        SqlDialectKind.SqlServer => new SqlServerDialect(),
        SqlDialectKind.MySql => new MySqlDialect(),
        SqlDialectKind.MariaDb => new MySqlDialect(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported SQL dialect.")
    };
}
