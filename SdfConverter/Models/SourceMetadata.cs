namespace SdfConverter.Models;

/// <summary>
/// Metadata about the source SDF file for SQL file header comments.
/// </summary>
/// <param name="SdfFileName">Original SDF filename</param>
/// <param name="TableName">Source table name (e.g., CHECKINOUT)</param>
/// <param name="RecordCount">Total records in source table</param>
public record SourceMetadata(
    string SdfFileName,
    string TableName,
    long RecordCount
);
