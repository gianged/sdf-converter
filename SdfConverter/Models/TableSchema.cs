using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>A table's full schema: name, row count, and columns.</summary>
public record TableSchema(
    string TableName,
    long RowCount,
    IReadOnlyList<ColumnInfo> Columns
);
