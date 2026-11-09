namespace UniversityLeaders.Application.Common;

/// <summary>Expected failures raised by services. The API maps each type to one HTTP status with a stable error code.</summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable code for clients (e.g. "invalid_credentials").</summary>
    public string Code { get; }
}

public sealed class ValidationException : AppException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more fields are invalid.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Deliberately generic: never says whether the user name exists, the password was wrong or the account is inactive.</summary>
public sealed class InvalidCredentialsException : AppException
{
    public InvalidCredentialsException()
        : base("invalid_credentials", "Invalid user name or password.")
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string code, string message)
        : base(code, message)
    {
    }
}

/// <summary>A dependency (e.g. object storage) is unavailable; the operation can be retried later.</summary>
public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string code, string message, Exception? inner = null)
        : base(code, message)
    {
        Inner = inner;
    }

    public Exception? Inner { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string code, string message)
        : base(code, message)
    {
    }
}
