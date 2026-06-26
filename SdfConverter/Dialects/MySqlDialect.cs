using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// MySQL / MariaDB dialect: backtick-quoted identifiers, 0/1 booleans, 0xHEX binary.
/// Strings also escape the backslash, which MySQL treats as an escape character.
/// </summary>
public sealed class MySqlDialect : SqlDialectBase
{
    public override string QuoteIdentifier(string name) =>
        $"`{name.Replace("`", "``")}`";

    // Bare table name; resolves against the connection's current database.
    public override string QualifyTable(string table) =>
        QuoteIdentifier(table);

    protected override string FormatBoolean(bool value) => value ? "1" : "0";

    protected override string FormatBinary(byte[] bytes) => $"0x{ToHex(bytes)}";

    protected override string QuoteString(string value) =>
        $"'{value.Replace("\\", "\\\\").Replace("'", "''")}'";

    public override string MapColumnType(ColumnInfo column) => column.DataType.ToLowerInvariant() switch
    {
        "int" => "INT",
        "bigint" => "BIGINT",
        "smallint" => "SMALLINT",
        "tinyint" => "TINYINT",
        "bit" => "TINYINT(1)",
        "float" => "DOUBLE",
        "real" => "FLOAT",
        "money" => "DECIMAL(19,4)",
        "numeric" or "decimal" => "DECIMAL",
        "datetime" => "DATETIME",
        "uniqueidentifier" => "CHAR(36)",
        "image" or "binary" or "varbinary" => "LONGBLOB",
        "ntext" or "text" => "LONGTEXT",
        _ => "TEXT"
    };
}
