# Phase 3: SdfReader

## Goal
Open and read data from SQL Server Compact Edition (.sdf) files.

## Files to Create
- `SdfReader.cs`

---

## Purpose
Extract attendance records from the SDF file for migration.

---

## Inputs
- SDF file path
- Table name (from SchemaDiscovery or user)
- Column mappings

## Outputs
- Stream of `AttendanceRecord` objects

---

## Requirements

### Open Connection
- Use `SqlCeConnection` from Microsoft.SqlServerCompact
- Connection string: `Data Source={sdf-file-path}`
- Handle file not found
- Handle file locked errors (suggest closing TAS ERP)

### Read Records
- Query selected table
- Map columns to `AttendanceRecord` using column mappings
- Support batch reading for memory efficiency
- Handle null values gracefully

### Data Conversion
- Convert datetime to DateTimeOffset (assume local timezone)
- Convert verify code to short
- Validate user ID is positive integer

### Error Cases
- File not found: clear error message
- File locked: suggest closing source application
- Invalid data: log error, skip record, continue

---

## Dependencies
- Phase 1 (Models) for AttendanceRecord
- Phase 2 (SchemaDiscovery) for column mappings
- NuGet: Microsoft.SqlServerCompact
