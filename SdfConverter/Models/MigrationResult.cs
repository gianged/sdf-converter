namespace SdfConverter.Models;

/// <summary>
/// Summary statistics after migration completes.
/// Used for reporting results to the user.
/// </summary>
/// <param name="TotalRecords">Records found in SDF</param>
/// <param name="InsertedCount">Successfully inserted</param>
/// <param name="DuplicateCount">Skipped (already exists)</param>
/// <param name="ErrorCount">Failed to insert</param>
public record MigrationResult(
    int TotalRecords,
    int InsertedCount,
    int DuplicateCount,
    int ErrorCount
);
