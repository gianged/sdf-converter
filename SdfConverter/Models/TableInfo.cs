namespace SdfConverter.Models;

/// <summary>
/// Represents metadata about a database table.
/// </summary>
/// <param name="TableName">Name of the table</param>
/// <param name="RowCount">Number of rows in the table</param>
public record TableInfo(
    string TableName,
    long RowCount
);
