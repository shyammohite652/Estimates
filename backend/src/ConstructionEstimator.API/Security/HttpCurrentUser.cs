using System.Security.Claims;
using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Domain.Users;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ConstructionEstimator.API.Security;

/// <summary>Resolves the caller from the validated JWT. Never from request bodies or headers the client controls.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context.");

    public Guid UserId => Guid.TryParse(Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
        ? id
        : throw new UnauthorizedAccessException("Token has no valid subject.");

    public UserRole Role => Enum.TryParse<UserRole>(Principal.FindFirstValue(AuthConstants.RoleClaim), out var role)
        ? role
        : throw new UnauthorizedAccessException("Token has no valid role.");
}
