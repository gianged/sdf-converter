namespace SdfConverter.Models;

/// <summary>A table name paired with its row count.</summary>
public record TableInfo(
    string TableName,
    long RowCount
);
