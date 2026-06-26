using SdfConverter.Models;

namespace SdfConverter.Dialects;

/// <summary>
/// Encodes how SQL is rendered for a specific target database dialect.
/// </summary>
public interface ISqlDialect
{
    /// <summary>Quotes a table or column identifier for this dialect.</summary>
    string QuoteIdentifier(string name);

    /// <summary>Builds a qualified table reference using this dialect's default schema.</summary>
    string QualifyTable(string table);

    /// <summary>Renders the opening of an idempotent CREATE TABLE statement, up to and including the '('.</summary>
    string CreateTableHeader(string table);

    /// <summary>Formats a value as a SQL literal based on its source data type.</summary>
    string FormatValue(object? value, string dataType);

    /// <summary>Maps a source SDF column type to this dialect's column type for DDL.</summary>
    string MapColumnType(ColumnInfo column);
}
