---
paths:
  - "SdfConverter/**/*.cs"
---

# Console output

This tool talks to the user through the console only; there is no logging framework. Keep new output consistent with the existing style.

- No emoji and no non-ASCII decoration in any output string. Output is read in a Windows console where these render poorly.
- Write normal progress/results to `Console.WriteLine` (stdout). Write errors only through the `WriteError` helper, which goes to `Console.Error` in red. See `Program.cs`.
- Gate detailed or diagnostic output behind `if (verbose)` so default runs stay quiet. Don't print column dumps or per-step detail unconditionally.
- Use color sparingly: only `ConsoleColor.Yellow` (warnings) and `ConsoleColor.Red` (errors), and always reset the color afterward.
- Report long-running progress through an `IProgress<int>` callback parameter, not by writing to the console from deep in the call stack. See the streaming export path in `SdfReader.cs` and `Program.cs`.
