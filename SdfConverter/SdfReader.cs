using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Linq;

using SdfConverter.Models;

namespace SdfConverter;

/// <summary>
/// Reads attendance records from SQL Server CE databases.
/// Converts raw database rows to AttendanceRecord objects with validation.
/// </summary>
public sealed class SdfReader
{
    private readonly SqlCeConnection _connection;
    private readonly SchemaDiscoveryResult _schema;

    /// <summary>
    /// Creates an SdfReader for the specified connection and schema.
    /// </summary>
    /// <param name="connection">Open SqlCeConnection from SchemaDiscovery</param>
    /// <param name="schema">Schema discovery result with table name and column mappings</param>
    /// <exception cref="ArgumentNullException">If connection or schema is null</exception>
    public SdfReader(SqlCeConnection connection, SchemaDiscoveryResult schema)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    /// <summary>
    /// Reads all attendance records from the configured table.
    /// Invalid records are skipped and reported in warnings.
    /// </summary>
    /// <param name="progress">Optional progress reporter (reports record count processed)</param>
    /// <returns>Result containing records, skip count, and warnings</returns>
    public SdfReadResult ReadRecords(IProgress<int>? progress = null)
    {
        var records = new List<AttendanceRecord>();
        var warnings = new List<string>();
        var skippedCount = 0;
        var processedCount = 0;

        // Find column mappings
        var deviceUidMapping = FindMapping("device_uid");
        var timestampMapping = FindMapping("timestamp");
        var verifyTypeMapping = FindMapping("verify_type");

        if (deviceUidMapping == null)
        {
            throw new InvalidOperationException("Required column mapping for 'device_uid' not found.");
        }

        if (timestampMapping == null)
        {
            throw new InvalidOperationException("Required column mapping for 'timestamp' not found.");
        }

        // Build SELECT query with mapped columns
        var columns = new List<string> { deviceUidMapping.SourceColumn, timestampMapping.SourceColumn };
        if (verifyTypeMapping != null)
        {
            columns.Add(verifyTypeMapping.SourceColumn);
        }

        var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
        var query = $"SELECT {columnList} FROM [{_schema.TableName}] ORDER BY [{timestampMapping.SourceColumn}]";

        using var cmd = new SqlCeCommand(query, _connection);
        using var reader = cmd.ExecuteReader();

        // Resolve column ordinals
        var deviceUidOrdinal = reader.GetOrdinal(deviceUidMapping.SourceColumn);
        var timestampOrdinal = reader.GetOrdinal(timestampMapping.SourceColumn);
        var verifyTypeOrdinal = verifyTypeMapping != null
            ? reader.GetOrdinal(verifyTypeMapping.SourceColumn)
            : -1;

        while (reader.Read())
        {
            processedCount++;

            var record = TryReadRecord(
                reader,
                deviceUidOrdinal,
                timestampOrdinal,
                verifyTypeOrdinal,
                processedCount,
                warnings
            );

            if (record != null)
            {
                records.Add(record);
            }
            else
            {
                skippedCount++;
            }

            // Report progress every 100 records
            if (processedCount % 100 == 0)
            {
                progress?.Report(processedCount);
            }
        }

        // Final progress report
        progress?.Report(processedCount);

        return new SdfReadResult(records, skippedCount, warnings);
    }

    /// <summary>
    /// Attempts to read and convert a single record from the data reader.
    /// </summary>
    /// <returns>AttendanceRecord if valid, null if invalid (warning added)</returns>
    private AttendanceRecord? TryReadRecord(
        SqlCeDataReader reader,
        int deviceUidOrdinal,
        int timestampOrdinal,
        int verifyTypeOrdinal,
        int rowNumber,
        List<string> warnings)
    {
        // Read device_uid
        if (reader.IsDBNull(deviceUidOrdinal))
        {
            warnings.Add($"Row {rowNumber}: Skipped - device_uid is null");
            return null;
        }

        int deviceUid;
        try
        {
            deviceUid = Convert.ToInt32(reader.GetValue(deviceUidOrdinal));
        }
        catch (Exception ex)
        {
            warnings.Add($"Row {rowNumber}: Skipped - device_uid conversion failed: {ex.Message}");
            return null;
        }

        if (deviceUid <= 0)
        {
            warnings.Add($"Row {rowNumber}: Skipped - device_uid must be positive (got {deviceUid})");
            return null;
        }

        // Read timestamp
        if (reader.IsDBNull(timestampOrdinal))
        {
            warnings.Add($"Row {rowNumber}: Skipped - timestamp is null");
            return null;
        }

        DateTimeOffset timestamp;
        try
        {
            var dateTime = reader.GetDateTime(timestampOrdinal);
            timestamp = new DateTimeOffset(dateTime, TimeZoneInfo.Local.GetUtcOffset(dateTime));
        }
        catch (Exception ex)
        {
            warnings.Add($"Row {rowNumber}: Skipped - timestamp conversion failed: {ex.Message}");
            return null;
        }

        // Read verify_type (optional, defaults to 0)
        short verifyType = 0;
        if (verifyTypeOrdinal >= 0 && !reader.IsDBNull(verifyTypeOrdinal))
        {
            try
            {
                verifyType = Convert.ToInt16(reader.GetValue(verifyTypeOrdinal));
            }
            catch (Exception ex)
            {
                warnings.Add($"Row {rowNumber}: Warning - verify_type conversion failed, using 0: {ex.Message}");
                // Continue with default value
            }
        }

        return new AttendanceRecord(deviceUid, timestamp, verifyType);
    }

    /// <summary>
    /// Finds a column mapping by target column name.
    /// </summary>
    private ColumnMapping? FindMapping(string targetColumn)
    {
        return _schema.Mappings.FirstOrDefault(m =>
            string.Equals(m.TargetColumn, targetColumn, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Exports a table to SQL file using streaming (constant memory usage).
    /// Reads and writes in batches without holding all records in memory.
    /// Recommended for large tables (100k+ records).
    /// </summary>
    /// <param name="connection">Open database connection</param>
    /// <param name="schema">Table schema with column metadata</param>
    /// <param name="outputPath">Path to output .sql file</param>
    /// <param name="writer">SqlWriter instance for formatting</param>
    /// <param name="metadata">Source metadata for file header</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Result containing export statistics</returns>
    public static StreamingExportResult ExportTableStreaming(
        SqlCeConnection connection,
        TableSchema schema,
        string outputPath,
        SqlWriter writer,
        SourceMetadata metadata,
        IProgress<int>? progress = null)
    {
        var warnings = new List<string>();
        var skippedCount = 0;
        var recordsWritten = 0;
        var batchCount = 0;
        var processedCount = 0;

        // Build SELECT query with all columns
        var columnList = string.Join(", ", schema.Columns.Select(c => $"[{c.ColumnName}]"));
        var query = $"SELECT {columnList} FROM [{schema.TableName}]";

        using var streamWriter = new System.IO.StreamWriter(outputPath, false, System.Text.Encoding.UTF8, bufferSize: 65536);

        // Write header
        writer.WriteHeader(streamWriter, metadata);

        using var cmd = new SqlCeCommand(query, connection);
        using var reader = cmd.ExecuteReader();

        // Build ordinal lookup for all columns
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

                // Write batch when full
                if (batch.Count >= SqlWriter.DefaultBatchSize)
                {
                    writer.WriteDynamicBatch(streamWriter, batch, schema);
                    recordsWritten += batch.Count;
                    batchCount++;
                    batch.Clear();

                    progress?.Report(processedCount);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Row {processedCount}: Skipped - {ex.Message}");
                skippedCount++;
            }
        }

        // Write remaining records
        if (batch.Count > 0)
        {
            writer.WriteDynamicBatch(streamWriter, batch, schema);
            recordsWritten += batch.Count;
            batchCount++;
        }

        // Final progress report
        progress?.Report(processedCount);

        // Flush and get file size
        streamWriter.Flush();
        var fileInfo = new System.IO.FileInfo(outputPath);
        var fileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

        return new StreamingExportResult(recordsWritten, skippedCount, batchCount, fileSizeBytes, warnings);
    }

    /// <summary>
    /// Reads all columns from a table without predefined mappings.
    /// WARNING: Loads all records into memory. For large tables (100k+), use ExportTableStreaming instead.
    /// </summary>
    /// <param name="connection">Open database connection</param>
    /// <param name="schema">Table schema with column metadata</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Result containing dynamic records</returns>
    public static DynamicReadResult ReadAllColumns(SqlCeConnection connection, TableSchema schema, IProgress<int>? progress = null)
    {
        var records = new List<DynamicRecord>();
        var warnings = new List<string>();
        var skippedCount = 0;
        var processedCount = 0;

        // Build SELECT query with all columns
        var columnList = string.Join(", ", schema.Columns.Select(c => $"[{c.ColumnName}]"));
        var query = $"SELECT {columnList} FROM [{schema.TableName}]";

        using var cmd = new SqlCeCommand(query, connection);
        using var reader = cmd.ExecuteReader();

        // Build ordinal lookup for all columns
        var ordinals = schema.Columns.ToDictionary(
            c => c.ColumnName,
            c => reader.GetOrdinal(c.ColumnName)
        );

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

                records.Add(new DynamicRecord(values));
            }
            catch (Exception ex)
            {
                warnings.Add($"Row {processedCount}: Skipped - {ex.Message}");
                skippedCount++;
            }

            // Report progress every 100 records
            if (processedCount % 100 == 0)
            {
                progress?.Report(processedCount);
            }
        }

        // Final progress report
        progress?.Report(processedCount);

        return new DynamicReadResult(records, skippedCount, warnings);
    }
}
