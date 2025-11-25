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

var rootCommand = new RootCommand("Convert SQL Server CE attendance data to PostgreSQL SQL")
{
    sdfFileArg,
    outputOption,
    tableOption,
    schemaOption,
    verboseOption
};

rootCommand.SetAction(parseResult =>
{
    var sdfFile = parseResult.GetValue(sdfFileArg)!;
    var outputFile = parseResult.GetValue(outputOption);
    var tableName = parseResult.GetValue(tableOption);
    var schemaName = parseResult.GetValue(schemaOption)!;
    var verbose = parseResult.GetValue(verboseOption);

    return RunExport(sdfFile, outputFile, tableName, schemaName, verbose);
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

    Console.WriteLine();
    var result = RunExport(sdfFile, null, null, "public", false);

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
/// Executes the SDF to SQL export workflow.
/// </summary>
/// <param name="sdfFile">Source SDF file</param>
/// <param name="outputFile">Output SQL file (null = derive from input)</param>
/// <param name="tableName">Table name override (null = auto-detect)</param>
/// <param name="schemaName">PostgreSQL schema name</param>
/// <param name="verbose">Enable verbose output</param>
/// <returns>Exit code (0 = success, non-zero = error)</returns>
static int RunExport(
    FileInfo sdfFile,
    FileInfo? outputFile,
    string? tableName,
    string schemaName,
    bool verbose)
{
    var outputPath = outputFile?.FullName
        ?? Path.ChangeExtension(sdfFile.FullName, ".sql");

    Console.WriteLine($"Opening: {sdfFile.Name}");

    try
    {
        using var discovery = new SchemaDiscovery(sdfFile.FullName);

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
