using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;

namespace ConstructionEstimator.Application.Security;

/// <summary>Data-level authorisation that a role policy cannot express (R18).</summary>
public sealed class AccessGuard(ICurrentUser currentUser, IProjectAccessRepository access)
{
    /// <summary>
    /// Throws <see cref="NotFoundException"/> (not Forbidden) when the caller is not assigned to the
    /// project, so estimate ids in other projects cannot be probed for existence (DESIGN.md A12).
    /// </summary>
    public async Task EnsureProjectAccessAsync(Guid projectId, string resource, object key, CancellationToken ct)
    {
        if (!await access.IsAssignedAsync(currentUser.UserId, projectId, ct))
            throw new NotFoundException(resource, key);
    }

    public static void Require(bool allowed, string action)
    {
        if (!allowed) throw new ForbiddenException($"Your role is not permitted to {action}.");
    }
}
