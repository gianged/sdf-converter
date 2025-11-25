using System;

namespace SdfConverter.Models;

/// <summary>
/// Represents a single attendance record during migration.
/// Maps to the attendance table in PostgreSQL.
/// </summary>
/// <param name="DeviceUid">Employee/user ID</param>
/// <param name="Timestamp">Check-in/out time</param>
/// <param name="VerifyType">Verification method code</param>
public record AttendanceRecord(
    int DeviceUid,
    DateTimeOffset Timestamp,
    short VerifyType
);
