namespace SdfConverter.Models;

/// <summary>Result of an SDF upgrade; carries the backup file path.</summary>
public record SdfUpgradeResult(
    string BackupFilePath
);
