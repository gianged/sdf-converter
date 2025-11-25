using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Error types that can occur during schema discovery.
/// </summary>
public enum SchemaDiscoveryErrorType
{
    /// <summary>No tables found in the SDF file.</summary>
    NoTablesFound,

    /// <summary>No table matches known attendance table patterns.</summary>
    NoAttendanceTableDetected,

    /// <summary>Required columns (device_uid, timestamp) are missing.</summary>
    RequiredColumnsMissing,

    /// <summary>Failed to connect to the SDF file.</summary>
    ConnectionFailed
}

/// <summary>
/// Detailed error information from schema discovery.
/// </summary>
/// <param name="ErrorType">Type of error encountered</param>
/// <param name="Message">Human-readable error message</param>
/// <param name="AvailableTables">Tables found (for NoAttendanceTableDetected)</param>
/// <param name="MissingColumns">Required columns not found</param>
public record SchemaDiscoveryError(
    SchemaDiscoveryErrorType ErrorType,
    string Message,
    IReadOnlyList<TableInfo>? AvailableTables = null,
    IReadOnlyList<string>? MissingColumns = null
);
