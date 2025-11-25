# Phase 1: Models

## Goal
Define data structures for attendance records and migration results.

## Files to Create
- `Models/AttendanceRecord.cs`
- `Models/MigrationResult.cs`

---

## AttendanceRecord

### Purpose
Represents a single attendance record during migration.

### Properties
| Property | Type | Description |
|----------|------|-------------|
| DeviceUid | int | Employee/user ID |
| Timestamp | DateTimeOffset | Check-in/out time |
| VerifyType | short | Verification method code |

### Notes
- Used to transfer data from SDF to PostgreSQL
- Maps to the `attendance` table in PostgreSQL

---

## MigrationResult

### Purpose
Summary statistics after migration completes.

### Properties
| Property | Type | Description |
|----------|------|-------------|
| TotalRecords | int | Records found in SDF |
| InsertedCount | int | Successfully inserted |
| DuplicateCount | int | Skipped (already exists) |
| ErrorCount | int | Failed to insert |

### Notes
- Displayed to user after migration
- Used for dry-run reporting

---

## Dependencies
None - this is the foundation for other phases.
