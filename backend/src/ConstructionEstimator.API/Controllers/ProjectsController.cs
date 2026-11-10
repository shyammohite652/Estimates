using ConstructionEstimator.API.Security;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstructionEstimator.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ProjectsController(ProjectService projects, EstimateService estimates) : ControllerBase
{
    /// <summary>Projects the caller is assigned to (R18), with their estimates and totals.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ProjectSummaryDto>> GetMine(CancellationToken ct) => projects.GetMyProjectsAsync(ct);

    /// <summary>Create a Draft estimate in a project (Estimator).</summary>
    [HttpPost("{projectId:guid}/estimates")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    [ProducesResponseType<EstimateDetailsDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEstimate(Guid projectId, CreateEstimateRequest request, CancellationToken ct)
    {
        var created = await estimates.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(EstimatesController.Get), "Estimates", new { id = created.Id }, created);
    }
}
