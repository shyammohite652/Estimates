using ConstructionEstimator.Domain.Users;

namespace ConstructionEstimator.Application.Security;

/// <summary>
/// R15/R17/R11: what each role may do, defined once. API policies and the UI's capability flags
/// are both derived from this table, so the rules cannot drift apart.
/// </summary>
public static class Permissions
{
    public static bool CanEditDraft(UserRole role) => role == UserRole.Estimator;
    public static bool CanCreateEstimate(UserRole role) => role == UserRole.Estimator;
    public static bool CanSubmit(UserRole role) => role == UserRole.Estimator;
    public static bool CanReopen(UserRole role) => role == UserRole.Estimator;
    public static bool CanReview(UserRole role) => role == UserRole.Reviewer;
    public static bool CanCorrectDescription(UserRole role) => role == UserRole.ProjectManager;

    /// <summary>R17: Estimators (and, by least privilege, Viewers) never see the labour component.</summary>
    public static bool CanViewLabourCost(UserRole role) =>
        role is UserRole.Reviewer or UserRole.ProjectManager;
}
