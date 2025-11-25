using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Represents the complete schema of a database table.
/// Used for dynamic column export without predefined mappings.
/// </summary>
/// <param name="TableName">Name of the table</param>
/// <param name="RowCount">Number of rows in the table</param>
/// <param name="Columns">All columns in the table with their metadata</param>
public record TableSchema(
    string TableName,
    long RowCount,
    IReadOnlyList<ColumnInfo> Columns
);
