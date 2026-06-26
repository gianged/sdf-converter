---
paths:
  - "SdfConverter/**/*.cs"
---

# SQL generation

Generated SQL is built by string interpolation into a `.sql` file (no parameterized driver on the target side). Because values are inlined, escaping is mandatory, not optional.

## Output goes through the dialect

- Rendering of the output SQL is delegated to `ISqlDialect` (in `Dialects/`). The writer never hardcodes a quote style: call `_dialect.QuoteIdentifier(name)`, `_dialect.QualifyTable(table)`, `_dialect.CreateTableHeader(table)`, and `_dialect.FormatValue(value, dataType)`. See `Writing/SqlWriter.cs`.
- Per-dialect differences (identifier quoting `"x"` / `[x]` / `` `x` ``, the hardcoded default schema, the idempotent CREATE TABLE header, boolean and binary literals, column types) live in each `*Dialect.cs`; the shared null/numeric/datetime/string logic and the default `CreateTableHeader` (`CREATE TABLE IF NOT EXISTS`) live in `SqlDialectBase`. Add a new behavior there, not in the writer.
- The source-side SELECT in `SdfReader.StreamTableInto` still uses SQL Server CE square-bracket quoting `[{columnName}]`, `[{tableName}]`, because it queries the `.sdf` (SQL CE), not the target dialect.

## Escaping and formatting (in `SqlDialectBase` / dialects)

- Escape string values by doubling single quotes before inlining: `value.Replace("'", "''")`. Never inline a raw DB string; this is the injection guard for the output file. `MySqlDialect` also escapes the backslash.
- Format numbers and other non-string values with `CultureInfo.InvariantCulture` so output never depends on the machine locale.
- Format datetimes with a fixed pattern via the dialect's `FormatDateTime`, never a locale-default `ToString()`.

## Assembly

- Build column lists with `string.Join(", ", ...)` and rows with interpolation. Don't switch to `StringBuilder` or `+` concatenation; the existing writers use interpolation + `Join` throughout.
