namespace SdfConverter.Models;

/// <summary>
/// Maps an SDF source column to a PostgreSQL target column.
/// </summary>
/// <param name="SourceColumn">Original column name in SDF</param>
/// <param name="TargetColumn">PostgreSQL column name</param>
/// <param name="SourceType">SQL Server CE data type</param>
public record ColumnMapping(
    string SourceColumn,
    string TargetColumn,
    string SourceType
);
