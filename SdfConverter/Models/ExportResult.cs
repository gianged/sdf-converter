namespace SdfConverter.Models;

/// <summary>
/// Results from SQL file generation.
/// </summary>
/// <param name="RecordsWritten">Total records written to SQL file</param>
/// <param name="FileSizeBytes">Output file size in bytes</param>
/// <param name="BatchCount">Number of INSERT batches generated</param>
public record ExportResult(
    int RecordsWritten,
    long FileSizeBytes,
    int BatchCount
);
