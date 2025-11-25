namespace SdfConverter.Models;

/// <summary>
/// Represents metadata about a database column.
/// </summary>
/// <param name="ColumnName">Original column name in SDF</param>
/// <param name="DataType">SQL Server CE data type</param>
/// <param name="IsNullable">Whether column allows NULL values</param>
/// <param name="OrdinalPosition">Column position (1-based)</param>
public record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int OrdinalPosition
);
