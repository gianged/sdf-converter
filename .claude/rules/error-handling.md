---
paths:
  - "SdfConverter/**/*.cs"
---

# Error handling

This is a CLI: lower layers throw, the command boundary catches and turns failures into exit codes. Follow the existing split.

## Expected failures: catch at the CLI boundary, map to an exit code

- Lower layers (`SchemaDiscovery`, `SdfUpgrader`, `SdfReader`) let provider exceptions propagate; they do not return error tuples. The boundary in `Program.cs` catches them with `when` filters (e.g. `catch (SqlCeException ex) when (IsPasswordRequired(ex))`) and decides what to do.
- Surface user-facing errors through the `WriteError` helper (red text to `Console.Error`, `Error: ` prefix), then return a non-zero exit code. See `Program.cs`.
- Per-row data errors during streaming are the exception to "abort on failure": `SdfReader.StreamTableInto` skips the offending row, records a warning, and keeps going. It still lets IO and catastrophic failures propagate.

## Exit codes

- Exit codes live in one place, the `ExitCode` enum (`ExitCode.cs`); the command handler returns `(int)ExitCode.X`. `Success = 0`, with a distinct non-zero value per failure class. Reuse an existing member for the same kind of failure; add a new one only for a genuinely new class.

## Exceptions: for the unexpected

- Validate public method inputs with `ArgumentNullException`, e.g. `_x = x ?? throw new ArgumentNullException(nameof(x))`. After validation, internal code may trust the value is non-null.
- Filter caught exceptions with `when` clauses instead of catching broadly and re-inspecting, e.g. `catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))`. This lets unrelated exceptions keep propagating.
- When wrapping an exception, pass the original as `innerException` (`throw new InvalidOperationException(msg, ex)`). Never swallow the cause.
