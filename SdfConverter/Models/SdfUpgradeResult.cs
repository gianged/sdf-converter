namespace SdfConverter.Models;

/// <summary>
/// Result of an SDF database upgrade operation.
/// </summary>
/// <param name="BackupFilePath">Path to the backup file created before upgrade</param>
/// <param name="UpgradePerformed">Whether an upgrade was actually performed</param>
public record SdfUpgradeResult(
    string BackupFilePath,
    bool UpgradePerformed
);
