namespace UniversityLeaders.Infrastructure.Options;

/// <summary>Bound from the "Database" section, e.g. env var Database__ConnectionString.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;
}
