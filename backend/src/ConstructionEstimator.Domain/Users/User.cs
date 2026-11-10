using ConstructionEstimator.Domain.Common;

namespace ConstructionEstimator.Domain.Users;

/// <summary>
/// R14 defines Estimator, Reviewer and Viewer. ProjectManager is added because R11 and R17
/// grant rights to a "project manager" that R14 does not define (see DESIGN.md A1).
/// </summary>
public enum UserRole
{
    Estimator = 1,
    Reviewer = 2,
    Viewer = 3,
    ProjectManager = 4,
}

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public UserRole Role { get; private set; }

    private User() { }

    public User(Guid id, string email, string displayName, UserRole role)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Email = Guard.RequiredText(email, nameof(email), 256).ToLowerInvariant();
        DisplayName = Guard.RequiredText(displayName, nameof(displayName), 128);
        Role = Enum.IsDefined(role) ? role : throw new DomainValidationException(nameof(role), "Unknown role.");
    }
}
