---
paths:
  - "SdfConverter/**/*.cs"
---

# SQL generation

Generated SQL is built by string interpolation into a `.sql` file (no parameterized driver on the Postgres side). Because values are inlined, escaping is mandatory, not optional.

## Identifiers and values

- Quote every identifier (table, column) with square brackets via interpolation: `[{columnName}]`, `[{tableName}]`. See `SqlWriter.cs` and `SdfReader.cs`.
- Escape string values by doubling single quotes before inlining: `value.Replace("'", "''")`. Never inline a raw DB/user string; this is the injection guard for the output file. See `SqlWriter.cs`.
- Format numbers and other non-string values with `CultureInfo.InvariantCulture` (e.g. `Convert.ToString(value, CultureInfo.InvariantCulture)`) so output never depends on the machine locale.
- Format timestamps with the fixed pattern `yyyy-MM-dd HH:mm:sszzz`, never a locale-default `ToString()`.

## Assembly

- Build column lists with `string.Join(", ", ...)` and rows with interpolation. Don't switch to `StringBuilder` or `+` concatenation; the existing writers use interpolation + `Join` throughout.
