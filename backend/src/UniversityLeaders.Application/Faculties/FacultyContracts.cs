namespace UniversityLeaders.Application.Faculties;

public sealed record CreateFacultyRequest(string? Name);

/// <summary><paramref name="Version"/> comes from the latest read; a stale value is rejected with 409.</summary>
public sealed record UpdateFacultyRequest(string? Name, uint? Version);

public sealed record FacultyResponse(int Id, string Name, uint Version);
