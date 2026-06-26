using System;

using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// SQL Server dialect: bracket-quoted identifiers, 0/1 booleans, 0xHEX binary.
/// </summary>
public sealed class SqlServerDialect : SqlDialectBase
{
    public override string QuoteIdentifier(string name) =>
        $"[{name.Replace("]", "]]")}]";

    // SQL Server's default schema is 'dbo'.
    public override string QualifyTable(string table) =>
        $"{QuoteIdentifier("dbo")}.{QuoteIdentifier(table)}";

    // SQL Server has no CREATE TABLE IF NOT EXISTS; guard with OBJECT_ID instead.
    public override string CreateTableHeader(string table)
    {
        var qualified = QualifyTable(table);
        return $"IF OBJECT_ID(N'{qualified.Replace("'", "''")}', N'U') IS NULL{Environment.NewLine}CREATE TABLE {qualified} (";
    }

    protected override string FormatBoolean(bool value) => value ? "1" : "0";

    protected override string FormatBinary(byte[] bytes) => $"0x{ToHex(bytes)}";

    public override string MapColumnType(ColumnInfo column) => column.DataType.ToLowerInvariant() switch
    {
        "int" => "int",
        "bigint" => "bigint",
        "smallint" => "smallint",
        "tinyint" => "tinyint",
        "bit" => "bit",
        "float" => "float",
        "real" => "real",
        "money" => "money",
        "numeric" or "decimal" => "numeric",
        "datetime" => "datetime",
        "uniqueidentifier" => "uniqueidentifier",
        "image" or "binary" or "varbinary" => "varbinary(max)",
        _ => "nvarchar(max)"
    };
}
