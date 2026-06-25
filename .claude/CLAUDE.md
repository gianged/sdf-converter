# SDF Converter

CLI tool that extracts tables from SQL Server Compact Edition (`.sdf`) files and writes PostgreSQL-compatible `.sql` files. Targets .NET Framework 4.8 / C# 12 because SQL Server CE libraries exist only for .NET Framework; Windows-only. See `README.md` for full usage/examples (not imported, to save context).

Pipeline: `.sdf -> SdfConverter.exe -> output.sql -> psql -> PostgreSQL`

## Commands
- Build: `dotnet build`
- Release exe (~2.4 MB): `dotnet publish -c Release` -> `bin/Release/net48/publish/SdfConverter.exe`
- Self-contained: `dotnet publish -c Release -r win-x64 --self-contained`
- Run: `SdfConverter.exe <sdf-file> [options]`; with no args (or double-click) it enters interactive prompt mode
- Tests: none in the repo

## Architecture
- `Program.cs`: CLI entry (System.CommandLine) plus interactive mode
- `SchemaDiscovery.cs`: auto-detects attendance tables (`CHECKINOUT`, `att_log`, `attendance`, `T_LOG`) and maps source columns to `device_uid` / `timestamp` / `verify_type`
- `SdfReader.cs`: streams rows from the `.sdf` (EntityFramework.SqlServerCompact), constant memory
- `SqlWriter.cs`: two output paths -- structured attendance export (`INSERT ... ON CONFLICT (device_uid, timestamp) DO NOTHING`, `source = 'sdf_migration'`) and dynamic any-table export (all columns as-is, `INSERT INTO {schema}.[Table] (...)`)
- `SdfUpgrader.cs`: upgrades legacy CE 2.0-3.5 files to 4.0 (writes `.sdf.backup`)
- `Models/`: C# record DTOs; `Polyfills/IsExternalInit.cs`: lets net48 compile records

## CLI options
`--output/-o`, `--table/-t` (repeatable), `--all-tables`, `--schema` (default `public`), `--verbose`, `--upgrade`, `--password/-p` (encrypted files)

## Conventions
- Keep targeting `net48` + `LangVersion 12.0`; do not "modernize" to .NET 5+ -- SQL CE requires .NET Framework
- net48 records depend on the existing `IsExternalInit` polyfill; don't delete it
- Native SQL CE DLLs are embedded by Costura.Fody, so the published build is a single `.exe`

## Workflow
- Conventional Commits required: release-please reads `feat:` / `fix:` / `perf:` to bump `<Version>` in `SdfConverter.csproj` and update `CHANGELOG.md`. Don't hand-edit the version or changelog.
