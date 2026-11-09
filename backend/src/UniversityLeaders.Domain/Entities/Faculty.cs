namespace UniversityLeaders.Domain.Entities;

public class Faculty
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Concurrency token (PostgreSQL xmin), maintained by the database.</summary>
    public uint Version { get; set; }
}
