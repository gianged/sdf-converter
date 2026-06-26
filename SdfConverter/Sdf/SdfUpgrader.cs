using System;
using System.Data.SqlServerCe;
using System.IO;

using SdfConverter.Models;

namespace SdfConverter.Sdf;

/// <summary>Upgrades legacy SQL Server CE files (2.0-3.5) to the 4.0 format.</summary>
public static class SdfUpgrader
{
    // SQL CE native error: file created by an earlier CE version, upgrade required.
    private const int UpgradeRequiredErrorCode = 25138;

    /// <summary>True if the exception means the file needs a version upgrade.</summary>
    public static bool IsUpgradeRequired(SqlCeException ex) =>
        ex.NativeError == UpgradeRequiredErrorCode;

    /// <summary>Copies the SDF to a .backup file and returns its path.</summary>
    public static string CreateBackup(string sdfFilePath)
    {
        var backupPath = $"{sdfFilePath}.backup";

        // Timestamp the backup name if one already exists.
        if (File.Exists(backupPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backupPath = $"{sdfFilePath}.{timestamp}.backup";
        }

        File.Copy(sdfFilePath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>Builds a SQL Server CE connection string, with the password if given.</summary>
    public static string BuildConnectionString(string sdfFilePath, string? password = null) =>
        string.IsNullOrEmpty(password)
            ? $"Data Source={sdfFilePath}"
            : $"Data Source={sdfFilePath};Password={password}";

    /// <summary>
    /// Upgrades an SDF file to the 4.0 format in place, backing it up first.
    /// On failure the original is restored from the backup.
    /// </summary>
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
            return new SdfUpgradeResult(backupPath);
        }
        catch (SqlCeException ex)
        {
            // Restore the original from backup before reporting failure.
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
