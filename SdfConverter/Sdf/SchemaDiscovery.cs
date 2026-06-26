using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.IO;

using SdfConverter.Models;

namespace SdfConverter.Sdf;

/// <summary>Reads tables, columns, and row counts from a SQL Server CE database.</summary>
public sealed class SchemaDiscovery : IDisposable
{
    private readonly SqlCeConnection _connection;
    private bool _disposed;

    /// <summary>Opens a connection to the .sdf file.</summary>
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

    /// <summary>Open connection for SdfReader to stream rows from.</summary>
    public SqlCeConnection Connection
    {
        get
        {
            ThrowIfDisposed();
            return _connection;
        }
    }

    /// <summary>Lists user tables with their row counts.</summary>
    public IReadOnlyList<TableInfo> ListTables()
    {
        ThrowIfDisposed();

        var tables = new List<TableInfo>();

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

        foreach (var tableName in tableNames)
        {
            var rowCount = GetTableRowCount(tableName);
            tables.Add(new TableInfo(tableName, rowCount));
        }

        return tables;
    }

    /// <summary>Gets a table's full schema (columns plus row count).</summary>
    public TableSchema GetTableSchema(string tableName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        }

        var columns = GetColumns(tableName);
        var rowCount = GetTableRowCount(tableName);

        return new TableSchema(tableName, rowCount, columns);
    }

    /// <summary>Gets column metadata for a table, ordered by position.</summary>
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

    private long GetTableRowCount(string tableName)
    {
        // Bracket quoting handles special characters in table names.
        using var cmd = new SqlCeCommand($"SELECT COUNT(*) FROM [{tableName}]", _connection);
        var result = cmd.ExecuteScalar();
        return Convert.ToInt64(result);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SchemaDiscovery));
        }
    }

    /// <summary>Closes the database connection.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
