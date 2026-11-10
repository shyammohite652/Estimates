using ConstructionEstimator.Domain.Common;

namespace ConstructionEstimator.Domain.Projects;

public sealed class Project
{
    private readonly List<ProjectAssignment> _assignments = [];

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Client { get; private set; }
    public IReadOnlyCollection<ProjectAssignment> Assignments => _assignments;

    private Project() { }

    public Project(Guid id, string code, string name, string? client)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Code = Guard.RequiredText(code, nameof(code), 32).ToUpperInvariant();
        Name = Guard.RequiredText(name, nameof(name), 200);
        Client = string.IsNullOrWhiteSpace(client) ? null : client.Trim();
    }

    /// <summary>R18: assigns a user to the project. Assigning twice is a no-op.</summary>
    public void Assign(Guid userId)
    {
        Guard.NotEmpty(userId, nameof(userId));
        if (_assignments.Any(a => a.UserId == userId)) return;
        _assignments.Add(new ProjectAssignment(Id, userId));
    }
}

/// <summary>R18: membership of a user in a project. A user only sees projects they are assigned to.</summary>
public sealed class ProjectAssignment
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }

    private ProjectAssignment() { }

    internal ProjectAssignment(Guid projectId, Guid userId)
    {
        ProjectId = projectId;
        UserId = userId;
    }
}
