# Phase 5: Program.cs (CLI)

## Goal
Wire all components together with command-line interface.

## Files to Create
- `Program.cs`

---

## Purpose
Entry point that orchestrates the export workflow.

---

## CLI Interface

### Usage
```
SdfConverter.exe <sdf-file> [options]
```

### Arguments
| Argument | Required | Description |
|----------|----------|-------------|
| sdf-file | Yes | Path to .sdf file |

### Options
| Option | Default | Description |
|--------|---------|-------------|
| --output, -o | (input).sql | Output .sql file path |
| --table | (auto) | Specify attendance table name |
| --schema | public | PostgreSQL schema name |
| --verbose | false | Show detailed progress |

---

## Workflow

1. Parse command-line arguments
2. Validate SDF file exists
3. Open SDF file
4. Run schema discovery (list tables)
5. Select attendance table (auto or from `--table`)
6. Read records from SDF
7. Write to SQL file
8. Display summary

---

## Output Format

### Discovery Phase (verbose)
```
Opening SDF file: C:\backup\attendance.sdf
Found 12 tables:
  - CHECKINOUT (45,230 rows)
  - EMPLOYEES (156 rows)
  ...
Auto-selected table: CHECKINOUT
```

### Export Phase
```
Exporting to: output.sql
  [10000/45230] 22.1%
  [20000/45230] 44.2%
  ...

Export complete:
  Total records: 45,230
  Output file: output.sql (2.3 MB)
```

---

## Error Handling
| Error | Action |
|-------|--------|
| SDF file not found | Exit with error message |
| No attendance table found | List tables, ask to use --table |
| Cannot write output file | Show permission/path error |
| Invalid arguments | Show usage help |

---

## Dependencies
- Phase 1 (Models)
- Phase 2 (SchemaDiscovery)
- Phase 3 (SdfReader)
- Phase 4 (SqlWriter)
- NuGet: System.CommandLine
