using System.Collections.Generic;

namespace SdfConverter.Models;

/// <summary>One row as a column-name to value map (null = SQL NULL).</summary>
public record DynamicRecord(
    IReadOnlyDictionary<string, object?> Values
);
