namespace SdfConverter.Models;

/// <summary>Source file info shown in the generated .sql header.</summary>
public record SourceMetadata(
    string SdfFileName,
    string TableName,
    long RecordCount
);
