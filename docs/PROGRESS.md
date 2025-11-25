# Development Progress

## Overview

| Phase | Component | Status | Files |
|-------|-----------|--------|-------|
| 1 | Models | Done | `Models/AttendanceRecord.cs`, `Models/MigrationResult.cs` |
| 2 | SchemaDiscovery | Done | `SchemaDiscovery.cs`, `Models/TableInfo.cs`, `Models/ColumnInfo.cs`, `Models/ColumnMapping.cs`, `Models/SchemaDiscoveryResult.cs`, `Models/SchemaDiscoveryError.cs` |
| 3 | SdfReader | Done | `SdfReader.cs`, `Models/SdfReadResult.cs` |
| 4 | SqlWriter | Pending | `SqlWriter.cs` |
| 5 | CLI | Pending | `Program.cs` |

---

## Phase 1: Models [DONE]

- [x] `AttendanceRecord` record (DeviceUid, Timestamp, VerifyType)
- [x] `MigrationResult` record (TotalRecords, InsertedCount, DuplicateCount, ErrorCount)
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

## Phase 4: SqlWriter [PENDING]

- [ ] Generate SQL file header with metadata
- [ ] Batched INSERT statements (1000 values per batch)
- [ ] ON CONFLICT DO NOTHING for duplicates
- [ ] Schema-qualified table names (--schema option)
- [ ] Track record counts for summary

---

## Phase 5: CLI [PENDING]

- [ ] System.CommandLine argument parsing
- [ ] --output, --table, --schema, --verbose options
- [ ] Progress display during export
- [ ] Summary output with record counts

---

## Notes

- Targets `net48` for SQL Server CE compatibility
- Windows-only due to SQL Server CE dependency
- Uses C# 12 with polyfills for modern syntax
