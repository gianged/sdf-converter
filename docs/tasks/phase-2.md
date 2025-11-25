# Phase 2: SchemaDiscovery

## Goal
Detect and analyze tables in the SDF file to find attendance data.

## Files to Create
- `SchemaDiscovery.cs`

---

## Purpose
Auto-detect the attendance table when user doesn't specify `--table` option.

---

## Inputs
- SQL Server CE connection (from SdfReader)

## Outputs
- List of tables with row counts
- Selected table name
- Column mappings

---

## Requirements

### List Tables
- Query all user tables in the SDF file
- Return table names with row counts
- Display to user in verbose mode

### Auto-Detect Attendance Table
Search for tables matching common names (case-insensitive):
- `CHECKINOUT`
- `att_log`
- `attendance`
- `T_LOG`

### Column Mapping
Map SDF columns to PostgreSQL columns (case-insensitive):

| SDF Variations | Maps To |
|----------------|---------|
| USERID, UserID, user_id, EmpID | device_uid |
| CHECKTIME, CheckTime, check_time, LogTime | timestamp |
| VERIFYCODE, VerifyCode, verify_code, VerifyType | verify_type |

### Error Cases
- No tables found: report error
- No attendance table detected: list available tables, prompt user to use `--table`
- Required columns missing: report which columns are missing

---

## Dependencies
- Phase 3 (SdfReader) for database connection
