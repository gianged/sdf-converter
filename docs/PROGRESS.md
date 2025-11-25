# Development Progress

## Overview

| Phase | Component | Status | Files |
|-------|-----------|--------|-------|
| 1 | Models | Done | `Models/AttendanceRecord.cs` |
| 2 | SchemaDiscovery | Done | `SchemaDiscovery.cs`, `Models/TableInfo.cs`, `Models/ColumnInfo.cs`, `Models/ColumnMapping.cs`, `Models/SchemaDiscoveryResult.cs`, `Models/SchemaDiscoveryError.cs` |
| 3 | SdfReader | Done | `SdfReader.cs`, `Models/SdfReadResult.cs` |
| 4 | SqlWriter | Done | `SqlWriter.cs`, `Models/SourceMetadata.cs`, `Models/ExportResult.cs` |
| 5 | CLI | Done | `Program.cs` |

---

## Phase 1: Models [DONE]

- [x] `AttendanceRecord` record (DeviceUid, Timestamp, VerifyType)
- [x] `IsExternalInit` polyfill for .NET Framework 4.8

---

## Phase 2: SchemaDiscovery [DONE]

- [x] List all tables with row counts (`ListTables()`)
- [x] Auto-detect attendance table (CHECKINOUT, att_log, attendance, T_LOG)
- [x] Column mapping (USERID -> device_uid, CHECKTIME -> timestamp, etc.)
- [x] Error handling for missing tables/columns
- [x] New models: `TableInfo`, `ColumnInfo`, `ColumnMapping`, `SchemaDiscoveryResult`, `SchemaDiscoveryError`
- [x] Connection ownership (SchemaDiscovery owns SqlCeConnection, exposes for SdfReader)

---

## Phase 3: SdfReader [DONE]

- [x] Receive SqlCeConnection from SchemaDiscovery
- [x] Read records using column mappings from SchemaDiscoveryResult
- [x] Convert datetime to DateTimeOffset (local timezone)
- [x] Validate device_uid > 0, skip invalid records with warnings
- [x] Progress reporting via `IProgress<int>`
- [x] New model: `SdfReadResult` (Records, SkippedCount, Warnings)

---

## Phase 4: SqlWriter [DONE]

- [x] Generate SQL file header with metadata (source, table, count, timestamp)
- [x] Batched INSERT statements (1000 values per batch)
- [x] ON CONFLICT DO NOTHING for duplicates
- [x] Schema-qualified table names (configurable schema)
- [x] Track record counts for summary
- [x] New models: `SourceMetadata` (SdfFileName, TableName, RecordCount), `ExportResult` (RecordsWritten, FileSizeBytes, BatchCount)

---

## Phase 5: CLI [DONE]

- [x] System.CommandLine argument parsing
- [x] --output, --table, --schema, --verbose options
- [x] Progress display during export
- [x] Summary output with record counts

---

## Notes

- Targets `net48` for SQL Server CE compatibility
- Windows-only due to SQL Server CE dependency
- Uses C# 12 with polyfills for modern syntax
