using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// SQLite dialect: double-quoted identifiers, no schema qualifier, 0/1 booleans, X'HEX' binary.
/// </summary>
public sealed class SqliteDialect : SqlDialectBase
{
    public override string QuoteIdentifier(string name) =>
        $"\"{name.Replace("\"", "\"\"")}\"";

    // SQLite has no schema namespace for ordinary tables.
    public override string QualifyTable(string table) =>
        QuoteIdentifier(table);

    protected override string FormatBoolean(bool value) => value ? "1" : "0";

    protected override string FormatBinary(byte[] bytes) => $"X'{ToHex(bytes)}'";

    public override string MapColumnType(ColumnInfo column) => column.DataType.ToLowerInvariant() switch
    {
        "int" or "bigint" or "smallint" or "tinyint" or "bit" => "INTEGER",
        "float" or "real" => "REAL",
        "money" or "numeric" or "decimal" => "NUMERIC",
        "image" or "binary" or "varbinary" => "BLOB",
        _ => "TEXT"
    };
}
