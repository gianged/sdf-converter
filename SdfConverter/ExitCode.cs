namespace SdfConverter;

/// <summary>
/// Process exit codes, one distinct value per failure class.
/// </summary>
internal enum ExitCode
{
    Success = 0,
    Failed = 1,
    NoInput = 2,
    WriteFailed = 3,
    UpgradeRequired = 4,
    UpgradeFailed = 5,
    PasswordRequired = 6,
    UnknownFormat = 7
}
