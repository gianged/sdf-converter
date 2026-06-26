using System;
using System.Globalization;

using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// Shared SQL rendering logic. Concrete dialects override only the parts that differ
/// (identifier quoting, table qualification, boolean/binary literals, type mapping).
/// </summary>
public abstract class SqlDialectBase : ISqlDialect
{
    /// <inheritdoc/>
    public abstract string QuoteIdentifier(string name);

    /// <inheritdoc/>
    public abstract string QualifyTable(string table);

    /// <inheritdoc/>
    public virtual string CreateTableHeader(string table) =>
        $"CREATE TABLE IF NOT EXISTS {QualifyTable(table)} (";

    /// <inheritdoc/>
    public abstract string MapColumnType(ColumnInfo column);

    /// <inheritdoc/>
    public string FormatValue(object? value, string dataType)
    {
        if (value == null || value == DBNull.Value)
        {
            return "NULL";
        }

        var lowerType = dataType.ToLowerInvariant();

        // Numeric types - output as-is using invariant culture
        if (lowerType is "int" or "bigint" or "smallint" or "tinyint" or "float" or "real" or "money" or "numeric" or "decimal")
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL";
        }

        if (lowerType == "bit")
        {
            return FormatBoolean(Convert.ToBoolean(value));
        }

        if (lowerType == "datetime")
        {
            return value is DateTime dt ? FormatDateTime(dt) : QuoteString(value.ToString() ?? "");
        }

        if (lowerType is "image" or "binary" or "varbinary")
        {
            return value is byte[] bytes ? FormatBinary(bytes) : "NULL";
        }

        // GUID and everything else fall back to a quoted string literal
        return QuoteString(value.ToString() ?? "");
    }

    /// <summary>Formats a boolean literal.</summary>
    protected abstract string FormatBoolean(bool value);

    /// <summary>Formats a binary literal from raw bytes.</summary>
    protected abstract string FormatBinary(byte[] bytes);

    /// <summary>Formats a datetime literal. Default is the SQL standard form.</summary>
    protected virtual string FormatDateTime(DateTime value) =>
        $"'{value:yyyy-MM-dd HH:mm:ss}'";

    /// <summary>Wraps a string in single quotes, escaping as the dialect requires.</summary>
    protected virtual string QuoteString(string value) =>
        $"'{value.Replace("'", "''")}'";

    /// <summary>Uppercase hex (no separators) for binary literals.</summary>
    protected static string ToHex(byte[] bytes) =>
        BitConverter.ToString(bytes).Replace("-", "");
}
