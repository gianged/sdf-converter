namespace SdfConverter.Models;

/// <summary>Metadata for one database column (type, nullability, 1-based position).</summary>
public record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int OrdinalPosition
);
