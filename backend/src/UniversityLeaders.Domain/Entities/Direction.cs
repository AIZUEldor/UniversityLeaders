namespace UniversityLeaders.Domain.Entities;

/// <summary>One of the seven official coordinator directions (fixed ids 1..7, seeded by migration).</summary>
public class Direction
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
