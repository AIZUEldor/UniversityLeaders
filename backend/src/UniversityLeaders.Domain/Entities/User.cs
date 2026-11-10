using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Domain.Entities;

/// <summary>
/// One account per person. Role decides which of FacultyId/DirectionId must be set (enforced by a DB check constraint).
/// Users are never deleted, only deactivated, so reports, reviews and audit history keep their author.
/// UserName is unique case-insensitively (citext).
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public int? DirectionId { get; set; }
    public Direction? Direction { get; set; }

    /// <summary>Hash only (never a plain or temporary password). Null until credentials are issued.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>True for one-time issued credentials; the user must set a new password on first login.</summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>Random value embedded in session cookies; rotating it (password change, logout) revokes every existing session.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Concurrency token (PostgreSQL xmin). Concurrent account updates fail instead of overwriting each other.</summary>
    public uint Version { get; set; }

    /// <summary>Consecutive wrong passwords since the last successful login, lockout or reset.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>While in the future, every login is refused (even with the right password).</summary>
    public DateTimeOffset? LockoutEndsAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
