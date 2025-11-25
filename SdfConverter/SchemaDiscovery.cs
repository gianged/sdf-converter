using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;

using SdfConverter.Models;

namespace SdfConverter;

/// <summary>
/// Discovers schema information from SQL Server CE databases.
/// Auto-detects attendance tables and maps columns to PostgreSQL schema.
/// </summary>
public sealed class SchemaDiscovery : IDisposable
{
    private readonly SqlCeConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Known attendance table name patterns (case-insensitive).
    /// </summary>
    private static readonly string[] KnownAttendanceTableNames =
    {
        "CHECKINOUT",
        "att_log",
        "attendance",
        "T_LOG"
    };

    /// <summary>
    /// Maps PostgreSQL target columns to known SDF source column variations.
    /// </summary>
    private static readonly Dictionary<string, string[]> ColumnVariations = new()
    {
        ["device_uid"] = new[] { "USERID", "UserID", "user_id", "EmpID", "emp_id", "EmployeeID", "employee_id" },
        ["timestamp"] = new[] { "CHECKTIME", "CheckTime", "check_time", "LogTime", "log_time", "DateTime", "AttTime", "att_time" },
        ["verify_type"] = new[] { "VERIFYCODE", "VerifyCode", "verify_code", "VerifyType", "verify_type", "VerifyMethod" }
    };

    /// <summary>
    /// Required target columns that must be mapped.
    /// </summary>
    private static readonly string[] RequiredTargetColumns = { "device_uid", "timestamp" };

    /// <summary>
    /// Creates a SchemaDiscovery instance for the specified SDF file.
    /// </summary>
    /// <param name="sdfFilePath">Path to the .sdf file</param>
    /// <param name="password">Optional database password for encrypted databases</param>
    /// <exception cref="ArgumentException">If path is null or empty</exception>
    /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
    public SchemaDiscovery(string sdfFilePath, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(sdfFilePath))
        {
            throw new ArgumentException("SDF file path cannot be null or empty.", nameof(sdfFilePath));
        }

        if (!File.Exists(sdfFilePath))
        {
            throw new FileNotFoundException($"SDF file not found: {sdfFilePath}", sdfFilePath);
        }

        var connectionString = SdfUpgrader.BuildConnectionString(sdfFilePath, password);
        _connection = new SqlCeConnection(connectionString);
        _connection.Open();
    }

    /// <summary>
    /// Gets the open SqlCeConnection for use by SdfReader.
    /// </summary>
    public SqlCeConnection Connection
    {
        get
        {
            ThrowIfDisposed();
            return _connection;
        }
    }

    /// <summary>
    /// Lists all user tables in the database with row counts.
    /// </summary>
    /// <returns>List of tables with metadata</returns>
    public IReadOnlyList<TableInfo> ListTables()
    {
        ThrowIfDisposed();

        var tables = new List<TableInfo>();

        // Get all user table names
        var tableNames = new List<string>();
        using (var cmd = new SqlCeCommand(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'TABLE' ORDER BY TABLE_NAME",
            _connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        // Get row count for each table
        foreach (var tableName in tableNames)
        {
            var rowCount = GetTableRowCount(tableName);
            tables.Add(new TableInfo(tableName, rowCount));
        }

        return tables;
    }

    /// <summary>
    /// Auto-detects the attendance table using known naming patterns.
    /// Searches for: CHECKINOUT, att_log, attendance, T_LOG (case-insensitive).
    /// </summary>
    /// <returns>Result tuple with either success result or error</returns>
    public (SchemaDiscoveryResult? Result, SchemaDiscoveryError? Error) AutoDetectTable()
    {
        ThrowIfDisposed();

        var tables = ListTables();

        if (tables.Count == 0)
        {
            return (null, new SchemaDiscoveryError(
                SchemaDiscoveryErrorType.NoTablesFound,
                "No tables found in the SDF file. The database may be empty or corrupted."
            ));
        }

        // Search for attendance table (case-insensitive)
        var detectedTable = tables.FirstOrDefault(t =>
            KnownAttendanceTableNames.Any(pattern =>
                string.Equals(t.TableName, pattern, StringComparison.OrdinalIgnoreCase)));

        if (detectedTable == null)
        {
            return (null, new SchemaDiscoveryError(
                SchemaDiscoveryErrorType.NoAttendanceTableDetected,
                "No attendance table detected. Use --table <name> to specify the table manually.",
                AvailableTables: tables
            ));
        }

        return MapColumns(detectedTable.TableName);
    }

    /// <summary>
    /// Maps columns from a specific table to PostgreSQL schema.
    /// </summary>
    /// <param name="tableName">Name of the table to map</param>
    /// <returns>Result tuple with either success result or error</returns>
    public (SchemaDiscoveryResult? Result, SchemaDiscoveryError? Error) MapColumns(string tableName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        }

        var columns = GetColumns(tableName);
        var columnNames = columns.Select(c => c.ColumnName).ToList();

        var mappings = new List<ColumnMapping>();
        var unmappedColumns = new List<string>(columnNames);

        // Try to map each target column
        foreach (var kvp in ColumnVariations)
        {
            var targetColumn = kvp.Key;
            var variations = kvp.Value;

            var matchedColumn = columnNames.FirstOrDefault(col =>
                variations.Any(v => string.Equals(col, v, StringComparison.OrdinalIgnoreCase)));

            if (matchedColumn != null)
            {
                var columnInfo = columns.First(c => c.ColumnName == matchedColumn);
                mappings.Add(new ColumnMapping(matchedColumn, targetColumn, columnInfo.DataType));
                unmappedColumns.Remove(matchedColumn);
            }
        }

        // Check required columns
        var missingRequired = RequiredTargetColumns
            .Where(req => !mappings.Any(m => m.TargetColumn == req))
            .ToList();

        if (missingRequired.Count > 0)
        {
            var missingDetails = missingRequired.Select(col =>
                $"{col} (expected: {string.Join(", ", ColumnVariations[col])})");

            return (null, new SchemaDiscoveryError(
                SchemaDiscoveryErrorType.RequiredColumnsMissing,
                $"Required columns missing in table '{tableName}':\n  {string.Join("\n  ", missingDetails)}\n\nAvailable columns: {string.Join(", ", columnNames)}",
                MissingColumns: missingRequired
            ));
        }

        var rowCount = GetTableRowCount(tableName);

        return (new SchemaDiscoveryResult(tableName, rowCount, mappings, unmappedColumns), null);
    }

    /// <summary>
    /// Gets column metadata for a specific table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>List of column metadata</returns>
    public IReadOnlyList<ColumnInfo> GetColumns(string tableName)
    {
        ThrowIfDisposed();

        var columns = new List<ColumnInfo>();

        using var cmd = new SqlCeCommand(
            "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION " +
            "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName ORDER BY ORDINAL_POSITION",
            _connection);

        cmd.Parameters.AddWithValue("@TableName", tableName);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new ColumnInfo(
                ColumnName: reader.GetString(0),
                DataType: reader.GetString(1),
                IsNullable: reader.GetString(2) == "YES",
                OrdinalPosition: reader.GetInt32(3)
            ));
        }

        return columns;
    }

    /// <summary>
    /// Gets the row count for a specific table.
    /// </summary>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Number of rows in the table</returns>
    private long GetTableRowCount(string tableName)
    {
        // Use bracket quoting to handle special characters in table names
        using var cmd = new SqlCeCommand($"SELECT COUNT(*) FROM [{tableName}]", _connection);
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Throws ObjectDisposedException if disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SchemaDiscovery));
        }
    }

    /// <summary>
    /// Disposes the database connection.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
