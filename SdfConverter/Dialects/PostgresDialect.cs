using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// PostgreSQL dialect: double-quoted identifiers, true/false booleans, '\xHEX' binary.
/// </summary>
public sealed class PostgresDialect : SqlDialectBase
{
    public override string QuoteIdentifier(string name) =>
        $"\"{name.Replace("\"", "\"\"")}\"";

    // PostgreSQL tables live in the default 'public' schema.
    public override string QualifyTable(string table) =>
        $"{QuoteIdentifier("public")}.{QuoteIdentifier(table)}";

    protected override string FormatBoolean(bool value) => value ? "true" : "false";

    protected override string FormatBinary(byte[] bytes) => $"'\\x{ToHex(bytes)}'";

    public override string MapColumnType(ColumnInfo column) => column.DataType.ToLowerInvariant() switch
    {
        "int" => "integer",
        "bigint" => "bigint",
        "smallint" => "smallint",
        "tinyint" => "smallint",
        "bit" => "boolean",
        "float" => "double precision",
        "real" => "real",
        "money" or "numeric" or "decimal" => "numeric",
        "datetime" => "timestamp",
        "uniqueidentifier" => "uuid",
        "image" or "binary" or "varbinary" => "bytea",
        _ => "text"
    };
}
