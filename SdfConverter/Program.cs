using System;
using System.CommandLine;
using System.Data.SqlServerCe;
using System.IO;

using SdfConverter;
using SdfConverter.Models;

// Interactive mode when no arguments provided (double-click scenario)
if (args.Length == 0)
{
    return RunInteractive();
}

// --- Command Definition ---
var sdfFileArg = new Argument<FileInfo>("sdf-file")
{
    Description = "Path to .sdf file"
}.AcceptExistingOnly();

var outputOption = new Option<FileInfo?>("--output", "-o")
{
    Description = "Output .sql file path"
};

var tableOption = new Option<string?>("--table")
{
    Description = "Specify attendance table name (auto-detect if omitted)"
};

var schemaOption = new Option<string>("--schema")
{
    Description = "PostgreSQL schema name",
    DefaultValueFactory = _ => "public"
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Show detailed progress"
};

var upgradeOption = new Option<bool>("--upgrade")
{
    Description = "Upgrade older SQL Server CE database format to 4.0 (creates backup)"
};

var passwordOption = new Option<string?>("--password", "-p")
{
    Description = "Database password for encrypted SDF files"
};

var rootCommand = new RootCommand("Convert SQL Server CE attendance data to PostgreSQL SQL")
{
    sdfFileArg,
    outputOption,
    tableOption,
    schemaOption,
    verboseOption,
    upgradeOption,
    passwordOption
};

rootCommand.SetAction(parseResult =>
{
    var sdfFile = parseResult.GetValue(sdfFileArg)!;
    var outputFile = parseResult.GetValue(outputOption);
    var tableName = parseResult.GetValue(tableOption);
    var schemaName = parseResult.GetValue(schemaOption)!;
    var verbose = parseResult.GetValue(verboseOption);
    var upgrade = parseResult.GetValue(upgradeOption);
    var password = parseResult.GetValue(passwordOption);

    return RunExport(sdfFile, outputFile, tableName, schemaName, verbose, upgrade, password);
});

return rootCommand.Parse(args).Invoke();

// --- Static Helper Methods ---

/// <summary>
/// Runs interactive mode when no command-line arguments provided.
/// </summary>
/// <returns>Exit code</returns>
static int RunInteractive()
{
    Console.WriteLine("SDF Converter - Convert attendance data to PostgreSQL SQL");
    Console.WriteLine();

    Console.Write("Enter path to .sdf file: ");
    var input = Console.ReadLine()?.Trim().Trim('"');

    if (string.IsNullOrEmpty(input))
    {
        WriteError("No file path provided.");
        WaitForKey();
        return 1;
    }

    // Try input as-is first, then check .exe's directory for just filename
    var sdfFile = new FileInfo(input);
    if (!sdfFile.Exists && !Path.IsPathRooted(input))
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var inExeDir = Path.Combine(exeDir, input);
        if (File.Exists(inExeDir))
        {
            sdfFile = new FileInfo(inExeDir);
        }
    }

    if (!sdfFile.Exists)
    {
        WriteError($"File not found: {input}");
        WaitForKey();
        return 1;
    }

    // Prompt for table selection
    string? tableName = null;
    string? password = null;
    var upgradePerformed = false;

    try
    {
        using var discovery = OpenWithUpgradePrompt(sdfFile.FullName, ref password, out upgradePerformed);
        var tables = discovery.ListTables();

        if (tables.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Available tables:");
            for (var i = 0; i < tables.Count; i++)
            {
                Console.WriteLine($"  [{i + 1}] {tables[i].TableName} ({tables[i].RowCount:N0} rows)");
            }
            Console.WriteLine();
            Console.Write("Select table number (or press Enter to auto-detect): ");
            var tableInput = Console.ReadLine()?.Trim();

            if (!string.IsNullOrEmpty(tableInput) && int.TryParse(tableInput, out var tableIndex))
            {
                if (tableIndex >= 1 && tableIndex <= tables.Count)
                {
                    tableName = tables[tableIndex - 1].TableName;
                }
                else
                {
                    WriteError($"Invalid table number. Must be between 1 and {tables.Count}.");
                    WaitForKey();
                    return 1;
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Export cancelled.");
        WaitForKey();
        return 1;
    }
    catch (InvalidOperationException ex) when (ex.InnerException is SqlCeException)
    {
        WriteError(ex.Message);
        WaitForKey();
        return 1;
    }
    catch (SqlCeException ex)
    {
        WriteError($"Failed to open SDF file: {ex.Message}");
        WaitForKey();
        return 1;
    }

    // Prompt for schema name
    Console.WriteLine();
    Console.Write("PostgreSQL schema name (default: public): ");
    var schemaInput = Console.ReadLine()?.Trim() ?? string.Empty;
    var schemaName = schemaInput.Length > 0 ? schemaInput : "public";

    Console.WriteLine();
    // Pass upgrade=true since we already handled it in interactive mode
    var result = RunExport(sdfFile, null, tableName, schemaName, false, upgradePerformed, password);

    WaitForKey();
    return result;
}

/// <summary>
/// Waits for user to press any key before exiting.
/// </summary>
static void WaitForKey()
{
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(true);
}

/// <summary>
/// Checks if the exception indicates a password is required.
/// </summary>
/// <param name="ex">The exception to check</param>
/// <returns>True if password is required</returns>
static bool IsPasswordRequired(SqlCeException ex) =>
    ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;

/// <summary>
/// Prompts for database password in interactive mode.
/// </summary>
/// <returns>Password entered by user, or null if empty</returns>
static string? PromptForPassword()
{
    Console.WriteLine();
    Console.Write("Enter database password: ");
    var password = Console.ReadLine()?.Trim();
    return string.IsNullOrEmpty(password) ? null : password;
}

/// <summary>
/// Opens a SchemaDiscovery, prompting user for upgrade consent and password if needed.
/// </summary>
/// <param name="sdfFilePath">Path to the SDF file</param>
/// <param name="password">Ref: database password (may be set if prompted)</param>
/// <param name="upgradePerformed">Output: whether upgrade was performed</param>
/// <returns>SchemaDiscovery instance</returns>
/// <exception cref="OperationCanceledException">If user declines upgrade</exception>
/// <exception cref="InvalidOperationException">If upgrade fails</exception>
static SchemaDiscovery OpenWithUpgradePrompt(string sdfFilePath, ref string? password, out bool upgradePerformed)
{
    upgradePerformed = false;

    try
    {
        return new SchemaDiscovery(sdfFilePath, password);
    }
    catch (SqlCeException ex) when (IsPasswordRequired(ex))
    {
        // Database is encrypted, prompt for password
        password = PromptForPassword();
        if (password == null)
        {
            throw new OperationCanceledException("No password provided for encrypted database.");
        }

        // Retry with password - may still need upgrade
        return OpenWithUpgradePrompt(sdfFilePath, ref password, out upgradePerformed);
    }
    catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Database was created with an older SQL Server CE version.");
        Console.ResetColor();
        Console.WriteLine("Upgrade is required to read this file.");
        Console.WriteLine("A backup will be created before upgrading.");
        Console.WriteLine();
        Console.Write("Upgrade now? (Y/N): ");

        var response = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (response != "Y" && response != "YES")
        {
            throw new OperationCanceledException("User declined database upgrade.");
        }

        Console.WriteLine();
        var upgradeResult = SdfUpgrader.Upgrade(sdfFilePath, password, msg => Console.WriteLine($"  {msg}"));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Database upgraded. Backup: {Path.GetFileName(upgradeResult.BackupFilePath)}");
        Console.ResetColor();

        upgradePerformed = true;
        return new SchemaDiscovery(sdfFilePath, password);
    }
}

/// <summary>
/// Executes the SDF to SQL export workflow.
/// </summary>
/// <param name="sdfFile">Source SDF file</param>
/// <param name="outputFile">Output SQL file (null = derive from input)</param>
/// <param name="tableName">Table name override (null = auto-detect)</param>
/// <param name="schemaName">PostgreSQL schema name</param>
/// <param name="verbose">Enable verbose output</param>
/// <param name="upgrade">Allow database upgrade if needed</param>
/// <param name="password">Database password for encrypted files</param>
/// <returns>Exit code (0 = success, non-zero = error)</returns>
static int RunExport(
    FileInfo sdfFile,
    FileInfo? outputFile,
    string? tableName,
    string schemaName,
    bool verbose,
    bool upgrade,
    string? password)
{
    var outputPath = outputFile?.FullName
        ?? Path.ChangeExtension(sdfFile.FullName, ".sql");

    Console.WriteLine($"Opening: {sdfFile.Name}");

    SchemaDiscovery discovery;
    try
    {
        discovery = new SchemaDiscovery(sdfFile.FullName, password);
    }
    catch (SqlCeException ex) when (IsPasswordRequired(ex))
    {
        WriteError("Database is password-protected. Use --password to provide the password.");
        return 6;
    }
    catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))
    {
        if (!upgrade)
        {
            WriteError("Database was created with an older SQL Server CE version and requires upgrade.");
            Console.WriteLine();
            Console.WriteLine("To upgrade the database (a backup will be created), use:");
            Console.WriteLine($"  SdfConverter.exe \"{sdfFile.FullName}\" --upgrade");
            return 4;
        }

        // Perform upgrade
        Action<string>? log = verbose ? msg => Console.WriteLine($"  {msg}") : null;
        try
        {
            var upgradeResult = SdfUpgrader.Upgrade(sdfFile.FullName, password, log);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Database upgraded. Backup: {Path.GetFileName(upgradeResult.BackupFilePath)}");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (InvalidOperationException upgradeEx)
        {
            WriteError(upgradeEx.Message);
            return 5;
        }

        // Retry after upgrade
        discovery = new SchemaDiscovery(sdfFile.FullName, password);
    }

    try
    {
        using (discovery)
        {
            // List tables in verbose mode
            if (verbose)
            {
                var tables = discovery.ListTables();
                Console.WriteLine($"\nFound {tables.Count} tables:");
                foreach (var table in tables)
                {
                    Console.WriteLine($"  - {table.TableName} ({table.RowCount:N0} rows)");
                }
            }

            // Select attendance table
            var (schemaResult, schemaError) = tableName != null
                ? discovery.MapColumns(tableName)
                : discovery.AutoDetectTable();

            if (schemaError != null)
            {
                return HandleSchemaError(schemaError);
            }

            var schema = schemaResult!;

            if (tableName != null)
            {
                Console.WriteLine($"Using table: {schema.TableName} ({schema.RowCount:N0} rows)");
            }
            else
            {
                Console.WriteLine($"Auto-detected table: {schema.TableName} ({schema.RowCount:N0} rows)");
            }

            // Display column mappings in verbose mode
            if (verbose)
            {
                Console.WriteLine("\nColumn mappings:");
                foreach (var mapping in schema.Mappings)
                {
                    Console.WriteLine($"  {mapping.SourceColumn} ({mapping.SourceType}) -> {mapping.TargetColumn}");
                }
            }

            // Read records
            Console.WriteLine("\nReading records...");
            var reader = new SdfReader(discovery.Connection, schema);
            var readProgress = CreateProgressReporter(schema.RowCount);
            var readResult = reader.ReadRecords(readProgress);
            Console.WriteLine(); // Newline after progress

            // Write SQL file
            Console.WriteLine($"\nWriting to: {Path.GetFileName(outputPath)}");
            var writer = new SqlWriter(schemaName);
            var metadata = new SourceMetadata(sdfFile.Name, schema.TableName, readResult.Records.Count);
            var writeProgress = CreateProgressReporter(readResult.Records.Count);
            var exportResult = writer.WriteToFile(outputPath, readResult.Records, metadata, writeProgress);
            Console.WriteLine(); // Newline after progress

            // Display summary
            DisplaySummary(readResult, exportResult, outputPath, verbose);

            return 0;
        }
    }
    catch (SqlCeException ex)
    {
        WriteError($"Failed to open SDF file: {ex.Message}");
        return 1;
    }
    catch (IOException ex)
    {
        WriteError($"Failed to write output file: {ex.Message}");
        return 3;
    }
}

/// <summary>
/// Creates a console progress reporter for export operations.
/// </summary>
/// <param name="totalRecords">Total records to process</param>
/// <returns>Progress reporter instance</returns>
static IProgress<int> CreateProgressReporter(long totalRecords)
{
    var lastReported = -1;
    return new Progress<int>(current =>
    {
        var percentage = totalRecords > 0
            ? (int)(current * 100 / totalRecords)
            : 0;

        // Only update when percentage changes to reduce console noise
        if (percentage != lastReported)
        {
            Console.Write($"\r  [{current:N0}/{totalRecords:N0}] {percentage}%");
            lastReported = percentage;
        }
    });
}

/// <summary>
/// Displays an error message to console with consistent formatting.
/// </summary>
/// <param name="message">Error message</param>
static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Error: {message}");
    Console.ResetColor();
}

/// <summary>
/// Handles schema discovery errors with appropriate console output.
/// </summary>
/// <param name="error">The schema discovery error</param>
/// <returns>Exit code 2</returns>
static int HandleSchemaError(SchemaDiscoveryError error)
{
    WriteError(error.Message);

    switch (error.ErrorType)
    {
        case SchemaDiscoveryErrorType.NoAttendanceTableDetected:
            if (error.AvailableTables?.Count > 0)
            {
                Console.WriteLine("\nAvailable tables:");
                foreach (var table in error.AvailableTables)
                {
                    Console.WriteLine($"  - {table.TableName} ({table.RowCount:N0} rows)");
                }
                Console.WriteLine("\nUse --table <name> to specify the attendance table.");
            }
            break;
    }

    return 2;
}

/// <summary>
/// Displays the export summary to console.
/// </summary>
/// <param name="readResult">Result from SdfReader</param>
/// <param name="exportResult">Result from SqlWriter</param>
/// <param name="outputPath">Output file path</param>
/// <param name="verbose">Whether verbose mode is enabled</param>
static void DisplaySummary(
    SdfReadResult readResult,
    ExportResult exportResult,
    string outputPath,
    bool verbose)
{
    Console.WriteLine("\nExport complete:");
    Console.WriteLine($"  Records exported: {exportResult.RecordsWritten:N0}");

    if (readResult.SkippedCount > 0)
    {
        Console.WriteLine($"  Records skipped:  {readResult.SkippedCount:N0}");
    }

    if (verbose)
    {
        Console.WriteLine($"  Batches written:  {exportResult.BatchCount:N0}");
    }

    Console.WriteLine($"  Output file:      {Path.GetFileName(outputPath)} ({FormatFileSize(exportResult.FileSizeBytes)})");

    // Display warnings if any
    if (readResult.Warnings.Count > 0)
    {
        Console.WriteLine("\nWarnings:");
        var maxWarnings = verbose ? readResult.Warnings.Count : Math.Min(5, readResult.Warnings.Count);

        for (var i = 0; i < maxWarnings; i++)
        {
            Console.WriteLine($"  - {readResult.Warnings[i]}");
        }

        if (!verbose && readResult.Warnings.Count > 5)
        {
            Console.WriteLine($"  ... and {readResult.Warnings.Count - 5} more (use --verbose to see all)");
        }
    }
}

/// <summary>
/// Formats byte count as human-readable file size.
/// </summary>
/// <param name="bytes">File size in bytes</param>
/// <returns>Formatted string (e.g., "2.3 MB")</returns>
static string FormatFileSize(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB"];
    var size = (double)bytes;
    var unitIndex = 0;

    while (size >= 1024 && unitIndex < units.Length - 1)
    {
        size /= 1024;
        unitIndex++;
    }

    return $"{size:0.#} {units[unitIndex]}";
}
