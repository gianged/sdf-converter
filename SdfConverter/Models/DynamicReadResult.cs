using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Result of reading dynamic records from a database table.
/// </summary>
/// <param name="Records">List of records read successfully</param>
/// <param name="SkippedCount">Number of records skipped due to errors</param>
/// <param name="Warnings">Warning messages for skipped or problematic records</param>
public record DynamicReadResult(
    IReadOnlyList<DynamicRecord> Records,
    int SkippedCount,
    IReadOnlyList<string> Warnings
);
