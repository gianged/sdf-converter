using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>
/// Represents a single database record with dynamic column values.
/// Used for exporting tables without predefined column mappings.
/// </summary>
/// <param name="Values">Dictionary mapping column names to their values (null for NULL values)</param>
public record DynamicRecord(
    IReadOnlyDictionary<string, object?> Values
);
