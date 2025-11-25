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
}
