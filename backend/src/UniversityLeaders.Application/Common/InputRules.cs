using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace UniversityLeaders.Application.Common;

/// <summary>Single source of truth for account input rules (used by auth and user management).</summary>
public static partial class InputRules
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128; // also caps hashing cost per request
    public const int MaxUserNameLength = 64;
    public const int MaxFullNameLength = 200;
    public const int MaxFacultyNameLength = 200;

    /// <summary>Returns the normalized (trimmed, lower-case) user name; adds an error when invalid.</summary>
    public static string UserName(string? value, Dictionary<string, string[]> errors, string field = "userName")
    {
        string userName = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (userName.Length < 3 || userName.Length > MaxUserNameLength || !UserNamePattern().IsMatch(userName))
        {
            errors[field] = new[] { $"User name must be 3-{MaxUserNameLength} characters: letters, digits, '.', '_' or '-'." };
        }

        return userName;
    }

    public static string RequiredText(string? value, int maxLength, string field, Dictionary<string, string[]> errors)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > maxLength)
        {
            errors[field] = new[] { $"Required, at most {maxLength} characters." };
        }

        return text;
    }

    /// <summary>Trimmed text or null when empty; longer than <paramref name="maxLength"/> is an error.</summary>
    public static string? OptionalText(string? value, int maxLength, string field, Dictionary<string, string[]> errors)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > maxLength)
        {
            errors[field] = new[] { $"At most {maxLength} characters." };
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Optional phone number: digits with optional +, spaces, dashes and brackets, 7-20 characters.</summary>
    public static string? Phone(string? value, Dictionary<string, string[]> errors, string field = "phone")
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length is < 7 or > 20 || !PhonePattern().IsMatch(trimmed))
        {
            errors[field] = new[] { "Use digits with optional +, spaces, dashes or brackets (7-20 characters)." };
        }

        return trimmed;
    }

    public static void NewPassword(string? password, string userName, string field, Dictionary<string, string[]> errors)
    {
        string value = password ?? string.Empty;
        if (value.Length < MinPasswordLength || value.Length > MaxPasswordLength)
        {
            errors[field] = new[] { $"Password must be {MinPasswordLength}-{MaxPasswordLength} characters." };
        }
        else if (userName.Length > 0 && value.Contains(userName, StringComparison.OrdinalIgnoreCase))
        {
            errors[field] = new[] { "Password must not contain the user name." };
        }
    }

    public static uint Version(uint? value, Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            errors["version"] = new[] { "Version is required (from the latest read of this record)." };
            return 0;
        }

        return value.Value;
    }

    /// <summary>Optimistic concurrency: the client's version must match the current row version.</summary>
    public static void EnsureVersion(uint current, uint expected)
    {
        if (current != expected)
        {
            throw new ConflictException("concurrent_update", "The record was changed by someone else. Reload and retry.");
        }
    }

    public static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    /// <summary>Random session-revocation stamp (64 hex chars).</summary>
    public static string NewSecurityStamp()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex UserNamePattern();

    [GeneratedRegex(@"^\+?[0-9][0-9 ()\-]*[0-9]$")]
    private static partial Regex PhonePattern();
}
