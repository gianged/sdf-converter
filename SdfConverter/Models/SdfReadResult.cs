using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Result of reading attendance records from an SDF file.
/// Contains successfully read records and details about skipped entries.
/// </summary>
/// <param name="Records">Successfully converted attendance records</param>
/// <param name="SkippedCount">Number of records skipped due to invalid data</param>
/// <param name="Warnings">Warning messages for skipped or problematic records</param>
public record SdfReadResult(
    IReadOnlyList<AttendanceRecord> Records,
    int SkippedCount,
    IReadOnlyList<string> Warnings
);
