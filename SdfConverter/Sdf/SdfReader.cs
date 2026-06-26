using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;

using SdfConverter.Models;
using SdfConverter.Writing;

namespace SdfConverter.Sdf;

/// <summary>
/// Streams table rows from a SQL Server CE database into an open SQL writer,
/// in batches so memory stays constant regardless of table size.
/// </summary>
public static class SdfReader
{
    /// <summary>
    /// Streams one table into an already-open writer. The caller owns the output
    /// stream, so several tables can share one file. FileSizeBytes is left to the caller.
    /// </summary>
    public static StreamingExportResult StreamTableInto(
        SqlWriter sqlWriter,
        StreamWriter output,
        SqlCeConnection connection,
        TableSchema schema,
        IProgress<int>? progress = null)
    {
        if (sqlWriter == null) throw new ArgumentNullException(nameof(sqlWriter));
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        var warnings = new List<string>();
        var skippedCount = 0;
        var recordsWritten = 0;
        var batchCount = 0;
        var processedCount = 0;

        // Source query uses SQL Server CE bracket quoting (not the target dialect).
        var columnList = string.Join(", ", schema.Columns.Select(c => $"[{c.ColumnName}]"));
        var query = $"SELECT {columnList} FROM [{schema.TableName}]";

        using var cmd = new SqlCeCommand(query, connection);
        using var reader = cmd.ExecuteReader();

        var ordinals = schema.Columns.ToDictionary(
            c => c.ColumnName,
            c => reader.GetOrdinal(c.ColumnName)
        );

        var batch = new List<DynamicRecord>(SqlWriter.DefaultBatchSize);

        while (reader.Read())
        {
            processedCount++;

            try
            {
                var values = new Dictionary<string, object?>();

                foreach (var column in schema.Columns)
                {
                    var ordinal = ordinals[column.ColumnName];
                    values[column.ColumnName] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
                }

                batch.Add(new DynamicRecord(values));

                if (batch.Count >= SqlWriter.DefaultBatchSize)
                {
                    sqlWriter.WriteDynamicBatch(output, batch, schema);
                    recordsWritten += batch.Count;
                    batchCount++;
                    batch.Clear();

                    progress?.Report(processedCount);
                }
            }
            catch (Exception ex) when (ex is SqlCeException or InvalidCastException or FormatException or OverflowException or ArgumentException)
            {
                warnings.Add($"Row {processedCount}: Skipped - {ex.Message}");
                skippedCount++;
            }
        }

        // Flush the final partial batch.
        if (batch.Count > 0)
        {
            sqlWriter.WriteDynamicBatch(output, batch, schema);
            recordsWritten += batch.Count;
            batchCount++;
        }

        progress?.Report(processedCount);

        return new StreamingExportResult(recordsWritten, skippedCount, batchCount, 0, warnings);
    }
}
