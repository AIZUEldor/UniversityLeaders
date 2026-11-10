namespace UniversityLeaders.Application.Faculties;

public sealed record CreateFacultyRequest(string? Name, string? Description);

/// <summary><paramref name="Version"/> comes from the latest read; a stale value is rejected with 409.</summary>
public sealed record UpdateFacultyRequest(string? Name, string? Description, uint? Version);

public sealed record FacultyResponse(int Id, string Name, string? Description, uint Version);
