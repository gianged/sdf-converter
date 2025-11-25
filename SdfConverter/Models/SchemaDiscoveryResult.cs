using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Result of a successful schema discovery operation.
/// Contains detected table name and column mappings.
/// </summary>
/// <param name="TableName">Detected attendance table name</param>
/// <param name="RowCount">Number of rows in the table</param>
/// <param name="Mappings">Column mappings from SDF to PostgreSQL</param>
/// <param name="UnmappedColumns">Columns that could not be mapped</param>
public record SchemaDiscoveryResult(
    string TableName,
    long RowCount,
    IReadOnlyList<ColumnMapping> Mappings,
    IReadOnlyList<string> UnmappedColumns
);
