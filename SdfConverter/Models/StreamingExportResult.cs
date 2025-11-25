using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Result of a streaming export operation.
/// Combines read and write statistics in one result.
/// </summary>
/// <param name="RecordsWritten">Number of records successfully written</param>
/// <param name="SkippedCount">Number of records skipped due to errors</param>
/// <param name="BatchCount">Number of INSERT batches written</param>
/// <param name="FileSizeBytes">Size of the output file in bytes</param>
/// <param name="Warnings">Warning messages for skipped or problematic records</param>
public record StreamingExportResult(
    int RecordsWritten,
    int SkippedCount,
    int BatchCount,
    long FileSizeBytes,
    IReadOnlyList<string> Warnings
);
