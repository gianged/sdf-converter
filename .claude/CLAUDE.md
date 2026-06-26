# SDF Converter

CLI tool that extracts every table/column from SQL Server Compact Edition (`.sdf`) files and writes `.sql` files for a chosen target dialect (SQLite default, PostgreSQL, SQL Server, MySQL, MariaDB). Targets .NET Framework 4.8 / C# 12 because SQL Server CE libraries exist only for .NET Framework; Windows-only. See `README.md` for full usage/examples (not imported, to save context).

Pipeline: `.sdf -> SdfConverter.exe -> {sdfname}.sql -> target database`

## Commands
- Build: `dotnet build`
- Release exe (~2.4 MB): `dotnet publish -c Release` -> `bin/Release/net48/publish/SdfConverter.exe`
- Self-contained: `dotnet publish -c Release -r win-x64 --self-contained`
- Run: `SdfConverter.exe -f <file|folder> [options]`; with no args (or double-click) it enters interactive prompt mode
- Tests: none in the repo

## Architecture
Files are grouped by responsibility, with each folder's namespace matching its path (`SdfConverter.Sdf`, `SdfConverter.Writing`, `SdfConverter.Dialects`, `SdfConverter.Models`). The `IsExternalInit` polyfill is the one exception (`System.Runtime.CompilerServices`, required for records on net48).
- `Program.cs`: CLI entry (System.CommandLine) plus interactive mode; resolves `-f` to one or more `.sdf` files and drives a combined per-database export
- `Sdf/SchemaDiscovery.cs`: lists tables, columns, and row counts from the `.sdf`
- `Sdf/SdfReader.cs`: static `StreamTableInto` streams one table's rows into an open writer, constant memory
- `Sdf/SdfUpgrader.cs`: upgrades legacy CE 2.0-3.5 files to 4.0 (writes `.sdf.backup`)
- `Writing/SqlWriter.cs`: `ExportDatabase` writes one combined `.sql` per file (header + per-table idempotent `CREATE TABLE` + INSERT batches); all rendering delegated to the dialect
- `Dialects/`: `ISqlDialect` + `SqlDialectBase` and one class per dialect (`SqlDialectFactory.Create` maps `SqlDialectKind`; MySQL and MariaDB share `MySqlDialect`). Encodes identifier quoting, table qualification (each dialect's hardcoded default schema: Postgres `public`, SQL Server `dbo`, MySQL/SQLite bare), the idempotent CREATE TABLE header, boolean/binary literals, and column-type mapping
- `Models/`: C# record DTOs; `Polyfills/IsExternalInit.cs`: lets net48 compile records

## CLI options
`--file/-f` (file or folder, default exe folder), `--table/-t` (repeatable; omitted = all tables), `--output/-o` (dialect: `sqlite` default | `postgres` | `mssql` | `mysql` | `mariadb`), `--verbose`, `--upgrade`, `--password/-p` (encrypted files). Output is one `{sdfname}.sql` beside each input, always including an idempotent `CREATE TABLE` per table.

## Conventions
- Keep targeting `net48` + `LangVersion 12.0`; do not "modernize" to .NET 5+ -- SQL CE requires .NET Framework
- net48 records depend on the existing `IsExternalInit` polyfill; don't delete it
- Native SQL CE DLLs are embedded by Costura.Fody, so the published build is a single `.exe`

## Workflow
- Conventional Commits required: release-please reads `feat:` / `fix:` / `perf:` to bump `<Version>` in `SdfConverter.csproj` and update `CHANGELOG.md`. Don't hand-edit the version or changelog.
