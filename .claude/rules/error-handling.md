---
paths:
  - "SdfConverter/**/*.cs"
---

# Error handling

This is a CLI: expected failures return data, unexpected ones throw. Follow the existing split.

## Expected failures: return, don't throw

- For foreseeable failures (schema/column discovery, mapping), return a tuple `(TResult? Result, TError? Error)` and have the caller check `Error != null`. See `SchemaDiscovery.cs`. Don't throw for these; the caller is meant to handle them inline.
- Surface user-facing errors through the `WriteError` helper (red text to `Console.Error`, `Error: ` prefix), then return a non-zero exit code. See `Program.cs`.

## Exit codes

- The command handler returns an `int` exit code: `0` on success, a distinct non-zero code per failure class. Reuse an existing code for the same kind of failure; add a new one only for a genuinely new class.

## Exceptions: for the unexpected

- Validate public method inputs with `ArgumentNullException`, e.g. `_x = x ?? throw new ArgumentNullException(nameof(x))`. After validation, internal code may trust the value is non-null.
- Filter caught exceptions with `when` clauses instead of catching broadly and re-inspecting, e.g. `catch (SqlCeException ex) when (SdfUpgrader.IsUpgradeRequired(ex))`. This lets unrelated exceptions keep propagating.
- When wrapping an exception, pass the original as `innerException` (`throw new InvalidOperationException(msg, ex)`). Never swallow the cause.
