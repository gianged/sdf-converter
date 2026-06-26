using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>Combined read/write statistics for a streaming export.</summary>
public record StreamingExportResult(
    int RecordsWritten,
    int SkippedCount,
    int BatchCount,
    long FileSizeBytes,
    IReadOnlyList<string> Warnings
);
