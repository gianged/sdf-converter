using System;
using System.Data.SqlServerCe;
using System.IO;

using SdfConverter.Models;

namespace SdfConverter;

/// <summary>
/// Handles SQL Server CE database version upgrades.
/// Upgrades older SDF files (v2.0, 3.0, 3.1, 3.5) to v4.0 format.
/// </summary>
public static class SdfUpgrader
{
    /// <summary>
    /// SQL CE native error code indicating version upgrade is required.
    /// Error: "The database file has been created by an earlier version of SQL Server Compact."
    /// </summary>
    private const int UpgradeRequiredErrorCode = 25138;

    /// <summary>
    /// Checks if the SqlCeException indicates a version upgrade is required.
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if upgrade is needed, false otherwise</returns>
    public static bool IsUpgradeRequired(SqlCeException ex) =>
        ex.NativeError == UpgradeRequiredErrorCode;

    /// <summary>
    /// Creates a backup of the SDF file before upgrade.
    /// </summary>
    /// <param name="sdfFilePath">Path to the original SDF file</param>
    /// <returns>Path to the backup file</returns>
    public static string CreateBackup(string sdfFilePath)
    {
        var backupPath = $"{sdfFilePath}.backup";

        // If backup already exists, add timestamp to avoid overwriting
        if (File.Exists(backupPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backupPath = $"{sdfFilePath}.{timestamp}.backup";
        }

        File.Copy(sdfFilePath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>
    /// Builds a SQL Server CE connection string with optional password.
    /// </summary>
    /// <param name="sdfFilePath">Path to the SDF file</param>
    /// <param name="password">Optional database password</param>
    /// <returns>Connection string</returns>
    public static string BuildConnectionString(string sdfFilePath, string? password = null) =>
        string.IsNullOrEmpty(password)
            ? $"Data Source={sdfFilePath}"
            : $"Data Source={sdfFilePath};Password={password}";

    /// <summary>
    /// Upgrades an SDF file to SQL Server CE 4.0 format.
    /// Creates a backup before performing the destructive in-place upgrade.
    /// </summary>
    /// <param name="sdfFilePath">Path to the SDF file to upgrade</param>
    /// <param name="password">Optional database password for encrypted databases</param>
    /// <param name="log">Optional callback for verbose logging</param>
    /// <param name="existingBackupPath">Optional path to existing backup (skips creating new backup)</param>
    /// <returns>Result containing backup path and upgrade status</returns>
    /// <exception cref="InvalidOperationException">If upgrade fails</exception>
    public static SdfUpgradeResult Upgrade(string sdfFilePath, string? password = null, Action<string>? log = null, string? existingBackupPath = null)
    {
        string backupPath;

        if (existingBackupPath != null && File.Exists(existingBackupPath))
        {
            log?.Invoke($"Using existing backup: {Path.GetFileName(existingBackupPath)}");
            backupPath = existingBackupPath;
        }
        else
        {
            log?.Invoke("Creating backup before upgrade...");
            backupPath = CreateBackup(sdfFilePath);
            log?.Invoke($"Backup created: {Path.GetFileName(backupPath)}");
        }

        try
        {
            log?.Invoke("Upgrading database to SQL Server CE 4.0 format...");

            var connectionString = BuildConnectionString(sdfFilePath, password);
            using var engine = new SqlCeEngine(connectionString);
            engine.Upgrade();

            log?.Invoke("Upgrade completed successfully.");
            return new SdfUpgradeResult(backupPath, true);
        }
        catch (SqlCeException ex)
        {
            // Restore from backup on failure
            log?.Invoke($"Upgrade failed: {ex.Message}");
            log?.Invoke("Restoring from backup...");

            try
            {
                File.Copy(backupPath, sdfFilePath, overwrite: true);
                log?.Invoke("Original file restored from backup.");
            }
            catch (IOException restoreEx)
            {
                throw new InvalidOperationException(
                    $"Upgrade failed and backup restoration also failed. " +
                    $"Manual recovery needed from: {backupPath}",
                    restoreEx);
            }

            throw new InvalidOperationException(
                $"Database upgrade failed: {ex.Message}. Original file has been restored.",
                ex);
        }
    }
}
