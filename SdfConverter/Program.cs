using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;

using SdfConverter;
using SdfConverter.Dialects;
using SdfConverter.Models;
using SdfConverter.Sdf;
using SdfConverter.Writing;

using static Cli;

// Interactive mode when no arguments provided (double-click scenario)
if (args.Length == 0)
{
    return RunInteractive();
}

// --- Command Definition ---
var fileOption = new Option<string?>("--file", "-f")
{
    Description = ".sdf file or folder to scan (default: the exe's folder)"
};

var tableOption = new Option<string[]>("--table", "-t")
{
    Description = "Table name(s) to export; repeatable. Omitted = all tables.",
    AllowMultipleArgumentsPerToken = true
};

var outputOption = new Option<string>("--output", "-o")
{
    Description = "Output SQL dialect: sqlite (default), postgres, mssql, mysql, mariadb",
    DefaultValueFactory = _ => "sqlite"
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

var rootCommand = new RootCommand("Convert SQL Server CE (.sdf) tables to SQL for a chosen dialect")
{
    fileOption,
    tableOption,
    outputOption,
    verboseOption,
    upgradeOption,
    passwordOption
};

rootCommand.SetAction(parseResult =>
{
    var fileOrFolder = parseResult.GetValue(fileOption);
    var tableNames = parseResult.GetValue(tableOption) ?? [];
    var outputFormat = parseResult.GetValue(outputOption)!;
    var verbose = parseResult.GetValue(verboseOption);
    var upgrade = parseResult.GetValue(upgradeOption);
    var password = parseResult.GetValue(passwordOption);

    if (!TryParseDialect(outputFormat, out var dialectKind))
    {
        WriteError($"Unknown output format '{outputFormat}'. Valid: sqlite, postgres, mssql, mysql, mariadb.");
        return (int)ExitCode.UnknownFormat;
    }

    var files = ResolveSdfFiles(fileOrFolder);
    if (files.Count == 0)
    {
        WriteError(string.IsNullOrWhiteSpace(fileOrFolder)
            ? $"No .sdf files found in {AppContext.BaseDirectory}"
            : $"No .sdf file(s) found at: {fileOrFolder}");
        return (int)ExitCode.NoInput;
    }

    return RunExport(files, tableNames, dialectKind, verbose, upgrade, password);
});

return rootCommand.Parse(args).Invoke();

internal static class Cli
{
    /// <summary>
    /// Resolves the -f/--file value to .sdf paths: empty scans the exe folder,
    /// a folder scans *.sdf, a file returns itself. Empty list if nothing matches.
    /// </summary>
    internal static List<string> ResolveSdfFiles(string? fileOrFolder)
    {
        if (string.IsNullOrWhiteSpace(fileOrFolder))
        {
            return EnumerateSdf(AppContext.BaseDirectory);
        }

        var trimmed = fileOrFolder!.Trim().Trim('"');

        if (Directory.Exists(trimmed))
        {
            return EnumerateSdf(trimmed);
        }

        if (File.Exists(trimmed))
        {
            return [Path.GetFullPath(trimmed)];
        }

        // Relative path didn't resolve against the working directory: try the exe folder.
        if (!Path.IsPathRooted(trimmed))
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, trimmed);
            if (File.Exists(candidate))
            {
                return [candidate];
            }

            if (Directory.Exists(candidate))
            {
                return EnumerateSdf(candidate);
            }
        }

        return [];
    }

    static List<string> EnumerateSdf(string directory) =>
        Directory.EnumerateFiles(directory, "*.sdf", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToList();

    /// <summary>Maps a friendly format name to a dialect (Sqlite when unrecognized); true if recognized.</summary>
    internal static bool TryParseDialect(string text, out SqlDialectKind kind)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "sqlite":
                kind = SqlDialectKind.Sqlite;
                return true;
            case "postgres":
            case "postgresql":
            case "pg":
                kind = SqlDialectKind.Postgres;
                return true;
            case "mssql":
            case "sqlserver":
            case "sql-server":
                kind = SqlDialectKind.SqlServer;
                return true;
            case "mysql":
                kind = SqlDialectKind.MySql;
                return true;
            case "mariadb":
                kind = SqlDialectKind.MariaDb;
                return true;
            default:
                kind = SqlDialectKind.Sqlite;
                return false;
        }
    }

    /// <summary>Runs the interactive prompt flow used when no arguments are passed.</summary>
    internal static int RunInteractive()
    {
        Console.WriteLine("SDF Converter - Convert SQL Server CE data to SQL");
        Console.WriteLine();

        Console.Write("Enter path to .sdf file or folder (Enter = scan this folder): ");
        var input = Console.ReadLine()?.Trim().Trim('"');

        var files = ResolveSdfFiles(string.IsNullOrEmpty(input) ? null : input);
        if (files.Count == 0)
        {
            WriteError("No .sdf files found.");
            WaitForKey();
            return (int)ExitCode.NoInput;
        }

        string[] tableNames = [];
        string? password = null;

        if (files.Count == 1)
        {
            try
            {
                using var discovery = OpenWithUpgradePrompt(files[0], ref password);
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
                    Console.WriteLine("  - Press Enter for all tables");
                    Console.Write("Choice: ");
                    var tableInput = Console.ReadLine()?.Trim();

                    if (!string.IsNullOrEmpty(tableInput))
                    {
                        var selectedTables = new List<string>();
                        var parts = tableInput!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var part in parts)
                        {
                            if (int.TryParse(part.Trim(), out var tableIndex) && tableIndex >= 1 && tableIndex <= tables.Count)
                            {
                                selectedTables.Add(tables[tableIndex - 1].TableName);
                            }
                            else
                            {
                                WriteError($"Invalid table selection: {part}. Expected a number between 1 and {tables.Count}.");
                                WaitForKey();
                                return (int)ExitCode.Failed;
                            }
                        }

                        tableNames = selectedTables.ToArray();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Export cancelled.");
                WaitForKey();
                return (int)ExitCode.Failed;
            }
            catch (InvalidOperationException ex) when (ex.InnerException is SqlCeException)
            {
                WriteError(ex.Message);
                WaitForKey();
                return (int)ExitCode.Failed;
            }
            catch (SqlCeException ex)
            {
                WriteError($"Failed to open SDF file: {ex.Message}");
                WaitForKey();
                return (int)ExitCode.Failed;
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"Found {files.Count} .sdf files; all tables of each will be exported.");
        }

        Console.WriteLine();
        Console.Write("Output format [sqlite, postgres, mssql, mysql, mariadb] (default: sqlite): ");
        var formatInput = Console.ReadLine()?.Trim();
        var dialectKind = SqlDialectKind.Sqlite;
        if (!string.IsNullOrEmpty(formatInput) && !TryParseDialect(formatInput!, out dialectKind))
        {
            Console.WriteLine($"Unknown format '{formatInput}'. Using sqlite.");
            dialectKind = SqlDialectKind.Sqlite;
        }

        Console.WriteLine();
        // upgrade: true here; older files upgrade automatically (single-file case already prompted).
        var result = RunExport(files, tableNames, dialectKind, verbose: false, upgrade: true, password);

        WaitForKey();
        return result;
    }

    static void WaitForKey()
    {
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey(true);
    }

    // True when the exception means a password is wrong or missing.
    static bool IsPasswordRequired(SqlCeException ex)
    {
        // SQL CE native errors: wrong password, or encrypted DB opened without one.
        const int InvalidPassword = 25028;
        const int EncryptedNoPassword = 25538;
        return ex.NativeError == InvalidPassword
            || ex.NativeError == EncryptedNoPassword
            || ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string? PromptForPassword()
    {
        Console.WriteLine();
        Console.Write("Enter database password: ");
        var password = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(password) ? null : password;
    }

    // Opens a SchemaDiscovery interactively, prompting for password and upgrade consent as needed.
    // Recurses after a prompt; throws OperationCanceledException if the user declines.
    static SchemaDiscovery OpenWithUpgradePrompt(string sdfFilePath, ref string? password, string? existingBackupPath = null)
    {
        try
        {
            return new SchemaDiscovery(sdfFilePath, password);
        }
        catch (SqlCeException ex) when (IsPasswordRequired(ex))
        {
            password = PromptForPassword();
            if (password == null)
            {
                throw new OperationCanceledException("No password provided for encrypted database.");
            }

            // Retry with password; may still need an upgrade.
            return OpenWithUpgradePrompt(sdfFilePath, ref password, existingBackupPath);
        }
        catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))
        {
            // Prompt for upgrade only on the first attempt (no backup yet).
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

            try
            {
                var upgradeResult = SdfUpgrader.Upgrade(sdfFilePath, password, msg => Console.WriteLine($"  {msg}"), existingBackupPath);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Database upgraded. Backup: {Path.GetFileName(upgradeResult.BackupFilePath)}");
                Console.ResetColor();

                return new SchemaDiscovery(sdfFilePath, password);
            }
            catch (InvalidOperationException upgradeEx) when (upgradeEx.InnerException is SqlCeException sqlEx && IsPasswordRequired(sqlEx))
            {
                // Upgrade needs a password; prompt and retry, reusing the backup to avoid a second copy.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Database is password-protected.");
                Console.ResetColor();

                var backupPath = $"{sdfFilePath}.backup";
                if (!File.Exists(backupPath))
                {
                    backupPath = null; // Let the next attempt create a fresh backup.
                }

                password = PromptForPassword();
                if (password == null)
                {
                    throw new OperationCanceledException("No password provided for encrypted database.");
                }

                return OpenWithUpgradePrompt(sdfFilePath, ref password, existingBackupPath: backupPath);
            }
        }
    }

    // Opens a SchemaDiscovery for non-interactive export, upgrading if allowed.
    // On failure, reports via WriteError and returns null with a non-zero errorCode.
    static SchemaDiscovery? OpenForExport(string sdfFilePath, bool upgrade, string? password, bool verbose, out int errorCode)
    {
        errorCode = 0;
        var fileName = Path.GetFileName(sdfFilePath);

        try
        {
            return new SchemaDiscovery(sdfFilePath, password);
        }
        catch (SqlCeException ex) when (IsPasswordRequired(ex))
        {
            WriteError($"{fileName} is password-protected. Use --password to provide the password.");
            errorCode = (int)ExitCode.PasswordRequired;
            return null;
        }
        catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))
        {
            if (!upgrade)
            {
                WriteError($"{fileName} was created with an older SQL Server CE version and requires upgrade. Use --upgrade.");
                errorCode = (int)ExitCode.UpgradeRequired;
                return null;
            }

            Action<string>? log = verbose ? msg => Console.WriteLine($"  {msg}") : null;
            try
            {
                var upgradeResult = SdfUpgrader.Upgrade(sdfFilePath, password, log);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Database upgraded. Backup: {Path.GetFileName(upgradeResult.BackupFilePath)}");
                Console.ResetColor();
            }
            catch (InvalidOperationException upgradeEx)
            {
                WriteError(upgradeEx.Message);
                errorCode = (int)ExitCode.UpgradeFailed;
                return null;
            }

            return new SchemaDiscovery(sdfFilePath, password);
        }
    }

    /// <summary>Exports each SDF file to a combined {sdfname}.sql beside it; returns an exit code.</summary>
    internal static int RunExport(
        IReadOnlyList<string> sdfFiles,
        string[] tableNames,
        SqlDialectKind dialectKind,
        bool verbose,
        bool upgrade,
        string? password)
    {
        var hadError = false;
        var lastError = 0;
        var filesExported = 0;
        var totalRecordsAll = 0;

        foreach (var rawPath in sdfFiles)
        {
            var sdfPath = Path.GetFullPath(rawPath);
            var fileName = Path.GetFileName(sdfPath);

            Console.WriteLine($"\nOpening: {fileName}");

            var discovery = OpenForExport(sdfPath, upgrade, password, verbose, out var openError);
            if (discovery == null)
            {
                hadError = true;
                lastError = openError;
                continue;
            }

            using (discovery)
            {
                try
                {
                    var availableTables = discovery.ListTables();
                    if (verbose)
                    {
                        Console.WriteLine($"Found {availableTables.Count} tables:");
                        foreach (var table in availableTables)
                        {
                            Console.WriteLine($"  - {table.TableName} ({table.RowCount:N0} rows)");
                        }
                    }

                    var selected = new List<TableInfo>();
                    if (tableNames.Length > 0)
                    {
                        foreach (var name in tableNames)
                        {
                            var match = availableTables.FirstOrDefault(t =>
                                string.Equals(t.TableName, name, StringComparison.OrdinalIgnoreCase));
                            if (match == null)
                            {
                                WriteError($"Table '{name}' not found in {fileName}.");
                            }
                            else
                            {
                                selected.Add(match);
                            }
                        }
                    }
                    else
                    {
                        selected.AddRange(availableTables);
                    }

                    if (selected.Count == 0)
                    {
                        WriteError($"No tables to export from {fileName}.");
                        hadError = true;
                        lastError = (int)ExitCode.NoInput;
                        continue;
                    }

                    var schemas = selected.Select(t => discovery.GetTableSchema(t.TableName)).ToList();
                    var totalRows = schemas.Sum(s => s.RowCount);
                    var tableSummary = schemas.Count == 1 ? schemas[0].TableName : $"{schemas.Count} tables";
                    var metadata = new SourceMetadata(fileName, tableSummary, totalRows);

                    var dialect = SqlDialectFactory.Create(dialectKind);
                    var writer = new SqlWriter(dialect);

                    var outputPath = Path.Combine(
                        Path.GetDirectoryName(sdfPath) ?? ".",
                        Path.GetFileNameWithoutExtension(sdfPath) + ".sql");

                    Console.WriteLine($"Exporting {schemas.Count} table(s) to {Path.GetFileName(outputPath)} [{dialectKind}]");

                    var result = writer.ExportDatabase(
                        discovery.Connection,
                        schemas,
                        outputPath,
                        metadata,
                        schema => CreateProgressReporter(schema.RowCount),
                        schema => Console.WriteLine($"\n--- Exporting: {schema.TableName} ({schema.RowCount:N0} rows) ---"));

                    Console.WriteLine(); // End the progress line.
                    DisplaySummary(result, outputPath, verbose);

                    filesExported++;
                    totalRecordsAll += result.RecordsWritten;
                }
                catch (SqlCeException ex)
                {
                    WriteError($"Failed to read {fileName}: {ex.Message}");
                    hadError = true;
                    lastError = (int)ExitCode.Failed;
                }
                catch (IOException ex)
                {
                    WriteError($"Failed to write output for {fileName}: {ex.Message}");
                    hadError = true;
                    lastError = (int)ExitCode.WriteFailed;
                }
            }
        }

        // Batch summary, only when more than one file was processed.
        if (sdfFiles.Count > 1)
        {
            Console.WriteLine("\n=== Batch Complete ===");
            Console.WriteLine($"  Files exported: {filesExported}");
            Console.WriteLine($"  Total records:  {totalRecordsAll:N0}");
        }

        return hadError ? lastError : 0;
    }

    // Console progress reporter that redraws only when the percentage changes.
    static IProgress<int> CreateProgressReporter(long totalRecords)
    {
        var lastReported = -1;
        return new Progress<int>(current =>
        {
            var percentage = totalRecords > 0
                ? (int)(current * 100 / totalRecords)
                : 0;

            if (percentage != lastReported)
            {
                Console.Write($"\r  [{current:N0}/{totalRecords:N0}] {percentage}%");
                lastReported = percentage;
            }
        });
    }

    /// <summary>Writes an error message to stderr in red.</summary>
    internal static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {message}");
        Console.ResetColor();
    }

    // Prints the per-file export summary, plus warnings (capped unless verbose).
    static void DisplaySummary(
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

    // Formats a byte count as a human-readable size (e.g. "2.3 MB").
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
}
