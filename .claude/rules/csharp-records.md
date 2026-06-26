---
paths:
  - "SdfConverter/**/*.cs"
---

# Records and DTOs

Data models live in `Models/` as positional C# records. Match this style for any new data carrier.

- Define DTOs as positional `record` types, one type per file under `Models/`. Positional params already become init-only public properties, so don't add separate `{ get; init; }` blocks. See `Models/ColumnInfo.cs`.
- Name the type and every positional parameter in PascalCase (the parameter name becomes the public property name).
- Make optional fields nullable with a `null` default (e.g. `SomeType? Extra = null`) instead of adding constructor overloads.
- Use records, not classes, for plain data carriers, and keep them immutable (no mutable setters). The code relies on the value equality and `with` expressions records provide.
