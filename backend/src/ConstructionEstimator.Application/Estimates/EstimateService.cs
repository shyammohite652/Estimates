using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Security;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.Application.Estimates;

/// <summary>
/// Estimate use cases. Order of checks in every command:
///   1. exists and caller is assigned to its project (R18)   → 404
///   2. caller's role permits the action (R15)               → 403
///   3. domain rules for the current state (R7–R13, R16)     → 409 / 422 / 403
/// </summary>
public sealed class EstimateService(
    ICurrentUser currentUser,
    AccessGuard guard,
    IEstimateRepository estimates,
    IEstimateQueries queries,
    ICatalogueRepository catalogue,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private bool CanSeeLabour => Permissions.CanViewLabourCost(currentUser.Role);

    // ------------------------------------------------------------------ queries

    public async Task<EstimateDetailsDto> GetAsync(Guid estimateId, CancellationToken ct)
    {
        var header = await queries.GetHeaderAsync(estimateId, ct) ?? throw new NotFoundException("Estimate", estimateId);
        await guard.EnsureProjectAccessAsync(header.ProjectId, "Estimate", estimateId, ct);
        return ToDetails(header);
    }

    public async Task<PagedResult<EstimateLineDto>> GetLinesAsync(Guid estimateId, int? page, int? pageSize, CancellationToken ct)
    {
        var paging = PageRequest.Create(page, pageSize);
        await LoadHeaderWithAccessAsync(estimateId, ct);
        return await queries.GetLinesAsync(estimateId, paging, CanSeeLabour, ct);
    }

    public async Task<IReadOnlyList<EstimateHistoryDto>> GetHistoryAsync(Guid estimateId, CancellationToken ct)
    {
        await LoadHeaderWithAccessAsync(estimateId, ct);
        return await queries.GetHistoryAsync(estimateId, ct);
    }

    // ------------------------------------------------------------------ commands

    public async Task<EstimateDetailsDto> CreateAsync(Guid projectId, CreateEstimateRequest request, CancellationToken ct)
    {
        await guard.EnsureProjectAccessAsync(projectId, "Project", projectId, ct);
        AccessGuard.Require(Permissions.CanCreateEstimate(currentUser.Role), "create estimates");

        var sequence = await estimates.CountEstimatesInProjectAsync(projectId, ct) + 1;
        var estimate = Estimate.Create(Guid.NewGuid(), projectId, $"EST-{sequence:0000}", request.Description,
            request.PricingDate, currentUser.UserId, Now);
        estimates.Add(estimate);
        await unitOfWork.SaveChangesAsync(ct); // unique (ProjectId, Number) turns a numbering race into 409
        return await GetAsync(estimate.Id, ct);
    }

    /// <summary>R7: adds a line, or adjusts the existing line for the same catalogue item.</summary>
    public async Task<LineMutationResult> AddOrAdjustLineAsync(Guid estimateId, AddLineRequest request, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanEditDraft(currentUser.Role), "edit estimate lines");

        var quantity = Quantity.Create(request.Quantity);
        var markup = MarkupPercentage.Create(request.MarkupPercent);
        var item = await catalogue.GetWithRatesAsync(request.CatalogueItemId, ct)
                   ?? throw new RequestValidationException("catalogueItemId", "Unknown catalogue item.");
        var existing = await estimates.GetLineForItemAsync(estimateId, item.Id, ct);

        var line = estimate.AddOrAdjustLine(item, existing, quantity, markup, Now);
        if (existing is null) estimates.AddLine(line);
        await unitOfWork.SaveChangesAsync(ct);

        var dto = await queries.GetLineAsync(estimateId, line.Id, CanSeeLabour, ct)
                  ?? throw new NotFoundException("Line", line.Id);
        return new LineMutationResult(dto, Created: existing is null);
    }

    public async Task<EstimateLineDto> UpdateLineAsync(Guid estimateId, long lineId, UpdateLineRequest request, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanEditDraft(currentUser.Role), "edit estimate lines");

        var line = await estimates.GetLineAsync(estimateId, lineId, ct) ?? throw new NotFoundException("Line", lineId);
        estimate.ChangeLine(line, Quantity.Create(request.Quantity), MarkupPercentage.Create(request.MarkupPercent), Now);
        await unitOfWork.SaveChangesAsync(ct);

        return await queries.GetLineAsync(estimateId, lineId, CanSeeLabour, ct) ?? throw new NotFoundException("Line", lineId);
    }

    public async Task RemoveLineAsync(Guid estimateId, long lineId, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanEditDraft(currentUser.Role), "edit estimate lines");

        var line = await estimates.GetLineAsync(estimateId, lineId, ct) ?? throw new NotFoundException("Line", lineId);
        estimate.RemoveLine(line, Now);
        estimates.RemoveLine(line);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EstimateDetailsDto> SubmitAsync(Guid estimateId, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanSubmit(currentUser.Role), "submit estimates");

        var lineCount = await estimates.CountLinesAsync(estimateId, ct);
        estimate.Submit(lineCount, Today, currentUser.UserId, Now);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(estimateId, ct);
    }

    /// <summary>
    /// R12: safe to retry. A sequential retry sees Approved and is a no-op. A concurrent duplicate
    /// loses the optimistic-concurrency race; it re-reads the status and, if the other request
    /// approved it, also succeeds without writing anything.
    /// </summary>
    public async Task<EstimateDetailsDto> ApproveAsync(Guid estimateId, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanReview(currentUser.Role), "approve estimates");

        if (estimate.Approve(currentUser.UserId, Now) == ApprovalOutcome.AlreadyApproved)
            return await GetAsync(estimateId, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConflictException)
        {
            unitOfWork.DiscardChanges();
            if (await queries.GetStatusAsync(estimateId, ct) != EstimateStatus.Approved) throw;
        }
        return await GetAsync(estimateId, ct);
    }

    public async Task<EstimateDetailsDto> RejectAsync(Guid estimateId, RejectEstimateRequest request, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanReview(currentUser.Role), "reject estimates");

        estimate.Reject(currentUser.UserId, request.Reason, Now);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(estimateId, ct);
    }

    public async Task<EstimateDetailsDto> ReopenAsync(Guid estimateId, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanReopen(currentUser.Role), "reopen estimates");

        estimate.Reopen(currentUser.UserId, Now);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(estimateId, ct);
    }

    /// <summary>R11: ProjectManager-only, any status, audited.</summary>
    public async Task<EstimateDetailsDto> CorrectDescriptionAsync(Guid estimateId, CorrectDescriptionRequest request, CancellationToken ct)
    {
        var estimate = await LoadForCommandAsync(estimateId, ct);
        AccessGuard.Require(Permissions.CanCorrectDescription(currentUser.Role), "correct estimate descriptions");

        if (estimate.CorrectDescription(request.Description, currentUser.UserId, Now))
            await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(estimateId, ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Estimate> LoadForCommandAsync(Guid estimateId, CancellationToken ct)
    {
        var estimate = await estimates.GetAsync(estimateId, ct) ?? throw new NotFoundException("Estimate", estimateId);
        await guard.EnsureProjectAccessAsync(estimate.ProjectId, "Estimate", estimateId, ct);
        return estimate;
    }

    private async Task LoadHeaderWithAccessAsync(Guid estimateId, CancellationToken ct)
    {
        var header = await queries.GetHeaderAsync(estimateId, ct) ?? throw new NotFoundException("Estimate", estimateId);
        await guard.EnsureProjectAccessAsync(header.ProjectId, "Estimate", estimateId, ct);
    }

    private EstimateDetailsDto ToDetails(EstimateHeaderData h)
    {
        var role = currentUser.Role;
        var capabilities = new EstimateCapabilities(
            CanEditLines: Permissions.CanEditDraft(role) && h.Status == EstimateStatus.Draft,
            CanSubmit: Permissions.CanSubmit(role) && h.Status == EstimateStatus.Draft,
            CanApprove: Permissions.CanReview(role) && h.Status == EstimateStatus.Submitted && h.CreatedById != currentUser.UserId,
            CanReject: Permissions.CanReview(role) && h.Status == EstimateStatus.Submitted,
            CanReopen: Permissions.CanReopen(role) && h.Status == EstimateStatus.Rejected,
            CanCorrectDescription: Permissions.CanCorrectDescription(role),
            CanViewLabourCost: CanSeeLabour);

        return new EstimateDetailsDto(h.Id, h.ProjectId, h.ProjectName, h.Number, h.Description, h.PricingDate,
            h.Status, h.CreatedById, h.CreatedByName, h.CreatedAt, h.UpdatedAt, h.LineCount, h.NetTotal,
            h.MarkupTotal, h.Total, CanSeeLabour ? h.LabourTotal : null, capabilities);
    }
}
