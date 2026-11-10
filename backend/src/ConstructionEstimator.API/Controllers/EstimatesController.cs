using ConstructionEstimator.API.Security;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Estimates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstructionEstimator.API.Controllers;

/// <summary>
/// Thin by design: bind, delegate, shape the HTTP response. All rules live in
/// EstimateService and the domain; all error mapping lives in ApiExceptionHandler.
/// </summary>
[ApiController]
[Route("api/estimates/{id:guid}")]
[Authorize]
public sealed class EstimatesController(EstimateService estimates) : ControllerBase
{
    /// <summary>Header, server-computed totals and the caller's capabilities. Lines are paged separately (R19).</summary>
    [HttpGet]
    public Task<EstimateDetailsDto> Get(Guid id, CancellationToken ct) => estimates.GetAsync(id, ct);

    /// <summary>A page of lines (default 100, max 500), in insertion order.</summary>
    [HttpGet("lines")]
    public Task<PagedResult<EstimateLineDto>> GetLines(Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        estimates.GetLinesAsync(id, page, pageSize, ct);

    [HttpGet("history")]
    public Task<IReadOnlyList<EstimateHistoryDto>> GetHistory(Guid id, CancellationToken ct) => estimates.GetHistoryAsync(id, ct);

    /// <summary>R7: adds a line, or adjusts the existing line for the same catalogue item. 201 if created, 200 if adjusted.</summary>
    [HttpPost("lines")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    [ProducesResponseType<EstimateLineDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<EstimateLineDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddLine(Guid id, AddLineRequest request, CancellationToken ct)
    {
        var result = await estimates.AddOrAdjustLineAsync(id, request, ct);
        return result.Created
            ? Created($"/api/estimates/{id}/lines/{result.Line.Id}", result.Line)
            : Ok(result.Line);
    }

    [HttpPut("lines/{lineId:long}")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    public Task<EstimateLineDto> UpdateLine(Guid id, long lineId, UpdateLineRequest request, CancellationToken ct) =>
        estimates.UpdateLineAsync(id, lineId, request, ct);

    [HttpDelete("lines/{lineId:long}")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveLine(Guid id, long lineId, CancellationToken ct)
    {
        await estimates.RemoveLineAsync(id, lineId, ct);
        return NoContent();
    }

    [HttpPost("submit")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    public Task<EstimateDetailsDto> Submit(Guid id, CancellationToken ct) => estimates.SubmitAsync(id, ct);

    /// <summary>Idempotent (R12): repeating it returns 200 and changes nothing.</summary>
    [HttpPost("approve")]
    [Authorize(Policy = AuthConstants.Policies.Review)]
    public Task<EstimateDetailsDto> Approve(Guid id, CancellationToken ct) => estimates.ApproveAsync(id, ct);

    [HttpPost("reject")]
    [Authorize(Policy = AuthConstants.Policies.Review)]
    public Task<EstimateDetailsDto> Reject(Guid id, RejectEstimateRequest request, CancellationToken ct) =>
        estimates.RejectAsync(id, request, ct);

    /// <summary>Rejected → Draft.</summary>
    [HttpPost("reopen")]
    [Authorize(Policy = AuthConstants.Policies.EditDrafts)]
    public Task<EstimateDetailsDto> Reopen(Guid id, CancellationToken ct) => estimates.ReopenAsync(id, ct);

    /// <summary>R11: ProjectManager corrects the description, in any status. Audited.</summary>
    [HttpPut("description")]
    [Authorize(Policy = AuthConstants.Policies.CorrectDescription)]
    public Task<EstimateDetailsDto> CorrectDescription(Guid id, CorrectDescriptionRequest request, CancellationToken ct) =>
        estimates.CorrectDescriptionAsync(id, request, ct);
}
