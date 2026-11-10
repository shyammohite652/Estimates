using ConstructionEstimator.Domain.Users;

namespace ConstructionEstimator.API.Security;

public static class AuthConstants
{
    public const string RoleClaim = "role";

    /// <summary>
    /// Coarse endpoint policies. They mirror Application.Security.Permissions; the application layer
    /// re-checks every rule (defence in depth), so a mis-applied attribute cannot open a hole.
    /// </summary>
    public static class Policies
    {
        public const string EditDrafts = nameof(EditDrafts);
        public const string Review = nameof(Review);
        public const string CorrectDescription = nameof(CorrectDescription);
    }

    public static readonly IReadOnlyDictionary<string, UserRole[]> PolicyRoles = new Dictionary<string, UserRole[]>
    {
        [Policies.EditDrafts] = [UserRole.Estimator],
        [Policies.Review] = [UserRole.Reviewer],
        [Policies.CorrectDescription] = [UserRole.ProjectManager],
    };
}
