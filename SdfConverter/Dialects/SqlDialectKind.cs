namespace SdfConverter.Dialects;

/// <summary>
/// Supported output SQL dialects. MySql and MariaDb share one implementation.
/// </summary>
public enum SqlDialectKind
{
    Postgres,
    Sqlite,
    SqlServer,
    MySql,
    MariaDb
}
