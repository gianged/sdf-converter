using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;

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

var tableOption = new Option<string[]>("--table", "-t")
{
    Description = "Table name(s) to export (can specify multiple: --table T1 --table T2)",
    AllowMultipleArgumentsPerToken = true
};

var allTablesOption = new Option<bool>("--all-tables")
{
    Description = "Export all tables from the database"
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

var rootCommand = new RootCommand("Convert SQL Server CE data to PostgreSQL SQL")
{
    sdfFileArg,
    outputOption,
    tableOption,
    allTablesOption,
    schemaOption,
    verboseOption,
    upgradeOption,
    passwordOption
};

rootCommand.SetAction(parseResult =>
{
    var sdfFile = parseResult.GetValue(sdfFileArg)!;
    var outputFile = parseResult.GetValue(outputOption);
    var tableNames = parseResult.GetValue(tableOption) ?? Array.Empty<string>();
    var allTables = parseResult.GetValue(allTablesOption);
    var schemaName = parseResult.GetValue(schemaOption)!;
    var verbose = parseResult.GetValue(verboseOption);
    var upgrade = parseResult.GetValue(upgradeOption);
    var password = parseResult.GetValue(passwordOption);

    return RunExport(sdfFile, outputFile, tableNames, allTables, schemaName, verbose, upgrade, password);
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
    var tableNames = Array.Empty<string>();
    var allTables = false;
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
            Console.WriteLine("Select table(s):");
            Console.WriteLine("  - Enter number(s) separated by commas (e.g., 1,3,5)");
            Console.WriteLine("  - Enter 'A' for all tables");
            Console.WriteLine("  - Press Enter to auto-detect");
            Console.Write("Choice: ");
            var tableInput = Console.ReadLine()?.Trim();

            if (!string.IsNullOrEmpty(tableInput))
            {
                if (tableInput!.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    allTables = true;
                }
                else
                {
                    // Parse comma-separated numbers
                    var selectedTables = new List<string>();
                    var parts = tableInput.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var part in parts)
                    {
                        if (int.TryParse(part.Trim(), out var tableIndex))
                        {
                            if (tableIndex >= 1 && tableIndex <= tables.Count)
                            {
                                selectedTables.Add(tables[tableIndex - 1].TableName);
                            }
                            else
                            {
                                WriteError($"Invalid table number: {tableIndex}. Must be between 1 and {tables.Count}.");
                                WaitForKey();
                                return 1;
                            }
                        }
                        else
                        {
                            WriteError($"Invalid input: {part}. Expected a number.");
                            WaitForKey();
                            return 1;
                        }
                    }

                    tableNames = selectedTables.ToArray();
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
    var result = RunExport(sdfFile, null, tableNames, allTables, schemaName, false, upgradePerformed, password);

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
/// <param name="existingBackupPath">Path to existing backup from previous upgrade attempt</param>
/// <returns>SchemaDiscovery instance</returns>
/// <exception cref="OperationCanceledException">If user declines upgrade</exception>
/// <exception cref="InvalidOperationException">If upgrade fails</exception>
static SchemaDiscovery OpenWithUpgradePrompt(string sdfFilePath, ref string? password, out bool upgradePerformed, string? existingBackupPath = null)
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
        return OpenWithUpgradePrompt(sdfFilePath, ref password, out upgradePerformed, existingBackupPath);
    }
    catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))
    {
        // Only prompt for upgrade if no backup exists (first attempt)
        if (existingBackupPath == null)
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
        }

        // Try upgrade - may fail if password is needed
        try
        {
            var upgradeResult = SdfUpgrader.Upgrade(sdfFilePath, password, msg => Console.WriteLine($"  {msg}"), existingBackupPath);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Database upgraded. Backup: {Path.GetFileName(upgradeResult.BackupFilePath)}");
            Console.ResetColor();

            upgradePerformed = true;
            return new SchemaDiscovery(sdfFilePath, password);
        }
        catch (InvalidOperationException upgradeEx) when (upgradeEx.InnerException is SqlCeException sqlEx && IsPasswordRequired(sqlEx))
        {
            // Upgrade failed due to password - prompt and retry, passing backup path to avoid double backup
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Database is password-protected.");
            Console.ResetColor();

            // Find the backup that was created (it's at the predictable path)
            var backupPath = $"{sdfFilePath}.backup";
            if (!File.Exists(backupPath))
            {
                backupPath = null; // Fallback: let next attempt create a new backup
            }

            password = PromptForPassword();
            if (password == null)
            {
                throw new OperationCanceledException("No password provided for encrypted database.");
            }

            // Retry with password and pass the existing backup path
            return OpenWithUpgradePrompt(sdfFilePath, ref password, out upgradePerformed, existingBackupPath: backupPath);
        }
    }
}

/// <summary>
/// Executes the SDF to SQL export workflow.
/// </summary>
/// <param name="sdfFile">Source SDF file</param>
/// <param name="outputFile">Output SQL file (null = derive from input)</param>
/// <param name="tableNames">Table names to export (empty = auto-detect)</param>
/// <param name="allTables">Export all tables</param>
/// <param name="schemaName">PostgreSQL schema name</param>
/// <param name="verbose">Enable verbose output</param>
/// <param name="upgrade">Allow database upgrade if needed</param>
/// <param name="password">Database password for encrypted files</param>
/// <returns>Exit code (0 = success, non-zero = error)</returns>
static int RunExport(
    FileInfo sdfFile,
    FileInfo? outputFile,
    string[] tableNames,
    bool allTables,
    string schemaName,
    bool verbose,
    bool upgrade,
    string? password)
{
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
            // List available tables
            var availableTables = discovery.ListTables();
            if (verbose)
            {
                Console.WriteLine($"\nFound {availableTables.Count} tables:");
                foreach (var table in availableTables)
                {
                    Console.WriteLine($"  - {table.TableName} ({table.RowCount:N0} rows)");
                }
            }

            // Determine which tables to export
            List<string> tablesToExport;

            if (allTables)
            {
                tablesToExport = availableTables.Select(t => t.TableName).ToList();
                Console.WriteLine($"\nExporting all {tablesToExport.Count} tables");
            }
            else if (tableNames.Length > 0)
            {
                tablesToExport = tableNames.ToList();
                Console.WriteLine($"\nExporting {tablesToExport.Count} specified table(s)");
            }
            else
            {
                // Auto-detect: try known attendance table patterns
                var detectedTable = availableTables.FirstOrDefault(t =>
                    new[] { "CHECKINOUT", "att_log", "attendance", "T_LOG" }
                        .Any(pattern => string.Equals(t.TableName, pattern, StringComparison.OrdinalIgnoreCase)));

                if (detectedTable != null)
                {
                    tablesToExport = new List<string> { detectedTable.TableName };
                    Console.WriteLine($"\nAuto-detected table: {detectedTable.TableName}");
                }
                else
                {
                    WriteError("No attendance table detected. Use --table <name> or --all-tables.");
                    if (availableTables.Count > 0)
                    {
                        Console.WriteLine("\nAvailable tables:");
                        foreach (var table in availableTables)
                        {
                            Console.WriteLine($"  - {table.TableName} ({table.RowCount:N0} rows)");
                        }
                    }
                    return 2;
                }
            }

            // Export each table
            var writer = new SqlWriter(schemaName);
            var totalRecords = 0;
            var totalFiles = 0;

            foreach (var tableName in tablesToExport)
            {
                // Verify table exists
                var tableInfo = availableTables.FirstOrDefault(t =>
                    string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase));

                if (tableInfo == null)
                {
                    WriteError($"Table '{tableName}' not found in database.");
                    continue;
                }

                // Determine output path
                string outputPath;
                if (outputFile != null && tablesToExport.Count == 1)
                {
                    outputPath = outputFile.FullName;
                }
                else
                {
                    // Multiple tables: use {sdfName}_{tableName}.sql
                    var baseName = Path.GetFileNameWithoutExtension(sdfFile.Name);
                    var directory = outputFile?.DirectoryName ?? Path.GetDirectoryName(sdfFile.FullName) ?? ".";
                    outputPath = Path.Combine(directory, $"{baseName}_{tableInfo.TableName}.sql");
                }

                Console.WriteLine($"\n--- Exporting: {tableInfo.TableName} ({tableInfo.RowCount:N0} rows) ---");

                // Get full table schema
                var schema = discovery.GetTableSchema(tableInfo.TableName);

                if (verbose)
                {
                    Console.WriteLine("Columns:");
                    foreach (var col in schema.Columns)
                    {
                        Console.WriteLine($"  {col.ColumnName} ({col.DataType})");
                    }
                }

                // Export using streaming (constant memory usage)
                Console.WriteLine($"Exporting to: {Path.GetFileName(outputPath)}");
                var metadata = new SourceMetadata(sdfFile.Name, schema.TableName, tableInfo.RowCount);
                var exportProgress = CreateProgressReporter(schema.RowCount);
                var exportResult = SdfReader.ExportTableStreaming(
                    discovery.Connection, schema, outputPath, writer, metadata, exportProgress);
                Console.WriteLine(); // Newline after progress

                // Display summary for this table
                DisplayStreamingSummary(exportResult, outputPath, verbose);

                totalRecords += exportResult.RecordsWritten;
                totalFiles++;
            }

            // Overall summary for multiple tables
            if (tablesToExport.Count > 1)
            {
                Console.WriteLine($"\n=== Export Complete ===");
                Console.WriteLine($"  Tables exported: {totalFiles}");
                Console.WriteLine($"  Total records:   {totalRecords:N0}");
            }

            return 0;
        }
    }
    catch (SqlCeException ex)
    {
        WriteError($"Failed to read SDF file: {ex.Message}");
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
/// Displays the export summary for streaming export.
/// </summary>
/// <param name="result">Result from streaming export</param>
/// <param name="outputPath">Output file path</param>
/// <param name="verbose">Whether verbose mode is enabled</param>
static void DisplayStreamingSummary(
    StreamingExportResult result,
    string outputPath,
    bool verbose)
{
    Console.WriteLine("Export complete:");
    Console.WriteLine($"  Records exported: {result.RecordsWritten:N0}");

    if (result.SkippedCount > 0)
    {
        Console.WriteLine($"  Records skipped:  {result.SkippedCount:N0}");
    }

    if (verbose)
    {
        Console.WriteLine($"  Batches written:  {result.BatchCount:N0}");
    }

    Console.WriteLine($"  Output file:      {Path.GetFileName(outputPath)} ({FormatFileSize(result.FileSizeBytes)})");

    // Display warnings if any
    if (result.Warnings.Count > 0)
    {
        Console.WriteLine("\nWarnings:");
        var maxWarnings = verbose ? result.Warnings.Count : Math.Min(5, result.Warnings.Count);

        for (var i = 0; i < maxWarnings; i++)
        {
            Console.WriteLine($"  - {result.Warnings[i]}");
        }

        if (!verbose && result.Warnings.Count > 5)
        {
            Console.WriteLine($"  ... and {result.Warnings.Count - 5} more (use --verbose to see all)");
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
