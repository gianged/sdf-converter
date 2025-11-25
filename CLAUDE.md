# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

One-time CLI tool to extract historical attendance data from SQL Server Compact Edition (`.sdf`) files and export to PostgreSQL-compatible `.sql` files.

**Workflow:** `SDF file → SdfConverter.exe → output.sql → psql → PostgreSQL`

## Build Commands

```bash
# Development build
dotnet build

# Release build (requires .NET Framework 4.8 runtime on target machine)
dotnet publish -c Release

# Self-contained single executable (~60-80 MB)
dotnet publish -c Release -r win-x64 --self-contained
```

## Architecture

```
SdfConverter/
├── Program.cs              # CLI entry point (System.CommandLine)
├── Models/                 # Data transfer objects (C# records)
├── Polyfills/              # .NET Framework compatibility shims
├── SdfReader.cs            # SDF file operations (EntityFramework.SqlServerCompact)
├── SqlWriter.cs            # SQL file generation
└── SchemaDiscovery.cs      # Table/column auto-detection
```

**Key design decisions:**
- Targets `net48` (.NET Framework 4.8) because SQL Server CE libraries only exist for .NET Framework
- Uses `LangVersion 12.0` with `IsExternalInit` polyfill to enable modern C# records
- Windows-only due to SQL Server CE dependency

## Data Flow

1. **SchemaDiscovery** - Auto-detects attendance table from common names (`CHECKINOUT`, `att_log`, `attendance`, `T_LOG`)
2. **SdfReader** - Reads records, maps varied column names (e.g., `USERID`/`UserID`/`EmpID` → `device_uid`)
3. **SqlWriter** - Generates batched `INSERT ... ON CONFLICT DO NOTHING` statements with `source = 'sdf_migration'`

## Target PostgreSQL Schema

Output SQL inserts into this schema:
```sql
CREATE TABLE attendance (
    device_uid INTEGER NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    verify_type SMALLINT NOT NULL,
    source VARCHAR(20) NOT NULL DEFAULT 'device',
    CONSTRAINT uq_attendance_record UNIQUE (device_uid, timestamp)
);
```

## CLI Usage

```bash
SdfConverter.exe <sdf-file> [options]

Options:
  --output, -o <path>    Output .sql file path
  --table <name>         Specify attendance table name (auto-detect if omitted)
  --schema <name>        PostgreSQL schema name (default: public)
  --verbose              Show detailed progress
```
