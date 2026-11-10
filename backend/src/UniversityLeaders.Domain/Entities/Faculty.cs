namespace UniversityLeaders.Domain.Entities;

public class Faculty
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional public subtitle, e.g. the programmes a faculty covers.</summary>
    public string? Description { get; set; }

    /// <summary>Concurrency token (PostgreSQL xmin), maintained by the database.</summary>
    public uint Version { get; set; }
}
