using System.Text.Json;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Pricing;
using ConstructionEstimator.Domain.Users;
using static ConstructionEstimator.UnitTests.Domain.TestData;

namespace ConstructionEstimator.UnitTests.Application;

/// <summary>R12, R15, R16, R17, R18 — authorisation and orchestration through the real service.</summary>
public class EstimateServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly World _world = new();

    private Estimate InProject(Estimate e)
    {
        // Re-create inside the fake world's project so that assignment (R18) checks apply.
        var copy = Estimate.Create(e.Id, _world.ProjectId, e.Number, e.Description, e.PricingDate, e.CreatedById, Now);
        return _world.Add(copy);
    }

    private Estimate SubmittedInProject(Guid createdBy)
    {
        var e = InProject(Draft(createdBy: createdBy));
        var item = Concrete();
        _world.Catalogue[item.Id] = item;
        _world.Lines.Add(e.AddOrAdjustLine(item, null, Quantity.Create(1m), MarkupPercentage.Create(0m), Now));
        e.Submit(1, Today, createdBy, Now);
        return e;
    }

    // ---------------------------------------------------------------- R18

    [Fact]
    public async Task Users_not_assigned_to_the_project_get_not_found_not_forbidden()
    {
        var estimate = InProject(Draft());
        var outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => _world.ServiceFor(outsider, UserRole.Reviewer).GetAsync(estimate.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _world.ServiceFor(outsider, UserRole.Reviewer).ApproveAsync(estimate.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _world.ServiceFor(outsider, UserRole.Estimator).GetLinesAsync(estimate.Id, 1, 50, Ct));
    }

    // ---------------------------------------------------------------- R15

    [Theory]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.Reviewer)]
    [InlineData(UserRole.ProjectManager)]
    public async Task Only_estimators_can_add_lines(UserRole role)
    {
        var estimate = InProject(Draft());
        var item = Concrete();
        _world.Catalogue[item.Id] = item;
        var user = Guid.NewGuid();
        _world.Assign(user);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.ServiceFor(user, role).AddOrAdjustLineAsync(estimate.Id, new AddLineRequest(item.Id, 1m, 0m), Ct));
        Assert.Empty(_world.Lines);
        Assert.Equal(0, _world.SaveCount);
    }

    [Theory]
    [InlineData(UserRole.Estimator)]
    [InlineData(UserRole.Viewer)]
    [InlineData(UserRole.ProjectManager)]
    public async Task Only_reviewers_can_approve(UserRole role)
    {
        var estimate = SubmittedInProject(Estimator);
        var user = Guid.NewGuid();
        _world.Assign(user);

        await Assert.ThrowsAsync<ForbiddenException>(() => _world.ServiceFor(user, role).ApproveAsync(estimate.Id, Ct));
        Assert.Equal(EstimateStatus.Submitted, estimate.Status);
    }

    [Fact]
    public async Task Only_project_managers_can_correct_descriptions()
    {
        var estimate = SubmittedInProject(Estimator);
        _world.Assign(Estimator);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.ServiceFor(Estimator, UserRole.Estimator).CorrectDescriptionAsync(estimate.Id, new("x"), Ct));
    }

    // ---------------------------------------------------------------- R16

    [Fact]
    public async Task Reviewer_who_created_the_estimate_cannot_approve_it_and_is_told_so_in_capabilities()
    {
        var estimate = SubmittedInProject(createdBy: Reviewer);
        _world.Assign(Reviewer);
        var service = _world.ServiceFor(Reviewer, UserRole.Reviewer);

        var details = await service.GetAsync(estimate.Id, Ct);
        Assert.False(details.Capabilities.CanApprove);
        Assert.True(details.Capabilities.CanReject);

        await Assert.ThrowsAsync<SelfApprovalNotAllowedException>(() => service.ApproveAsync(estimate.Id, Ct));
        Assert.Equal(EstimateStatus.Submitted, estimate.Status);
    }

    // ---------------------------------------------------------------- R12

    [Fact]
    public async Task Repeated_approval_returns_success_without_saving_again()
    {
        var estimate = SubmittedInProject(Estimator);
        _world.Assign(Reviewer);
        var service = _world.ServiceFor(Reviewer, UserRole.Reviewer);

        await service.ApproveAsync(estimate.Id, Ct);
        var second = await service.ApproveAsync(estimate.Id, Ct);

        Assert.Equal(EstimateStatus.Approved, second.Status);
        Assert.Equal(1, _world.SaveCount);
        Assert.Single(estimate.History, h => h.Action == EstimateAction.Approved);
    }

    [Fact]
    public async Task Approval_that_loses_a_concurrency_race_to_another_approval_still_succeeds()
    {
        var estimate = SubmittedInProject(Estimator);
        _world.Assign(Reviewer);
        // Simulate: a parallel request approved and saved first; ours hits the rowversion conflict.
        _world.SaveFailures.Enqueue(new ConflictException("rowversion"));

        var result = await _world.ServiceFor(Reviewer, UserRole.Reviewer).ApproveAsync(estimate.Id, Ct);

        // The in-memory estimate already reads Approved, standing in for the winner's committed row.
        Assert.Equal(EstimateStatus.Approved, result.Status);
    }

    [Fact]
    public async Task Approval_conflict_that_did_not_end_in_approval_is_surfaced()
    {
        var estimate = SubmittedInProject(Estimator);
        _world.Assign(Reviewer);
        _world.SaveFailures.Enqueue(new ConflictException("rowversion"));
        // The committed row says someone else rejected it first: this is a real conflict, not a duplicate.
        _world.StatusOverride = EstimateStatus.Rejected;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _world.ServiceFor(Reviewer, UserRole.Reviewer).ApproveAsync(estimate.Id, Ct));
    }

    // ---------------------------------------------------------------- R17

    [Fact]
    public async Task Estimators_never_receive_labour_cost()
    {
        var estimate = SubmittedInProject(Estimator);
        _world.Assign(Estimator);
        var service = _world.ServiceFor(Estimator, UserRole.Estimator);

        var details = await service.GetAsync(estimate.Id, Ct);
        var lines = await service.GetLinesAsync(estimate.Id, 1, 50, Ct);

        Assert.False(_world.LastIncludeLabour);
        Assert.Null(details.LabourTotal);
        Assert.False(details.Capabilities.CanViewLabourCost);
        Assert.All(lines.Items, l => Assert.Null(l.UnitLabourCost));

        // And the field is absent from the wire format, not merely null.
        var json = JsonSerializer.Serialize(lines.Items[0], JsonSerializerOptions.Web);
        Assert.DoesNotContain("labour", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"labourTotal\"", JsonSerializer.Serialize(details, JsonSerializerOptions.Web));
    }

    [Theory]
    [InlineData(UserRole.Reviewer)]
    [InlineData(UserRole.ProjectManager)]
    public async Task Reviewers_and_project_managers_receive_labour_cost(UserRole role)
    {
        var estimate = SubmittedInProject(Estimator);
        var user = Guid.NewGuid();
        _world.Assign(user);
        var service = _world.ServiceFor(user, role);

        var details = await service.GetAsync(estimate.Id, Ct);
        var lines = await service.GetLinesAsync(estimate.Id, 1, 50, Ct);

        Assert.True(_world.LastIncludeLabour);
        Assert.Equal(320.00m, details.LabourTotal);
        Assert.Equal(320.00m, lines.Items[0].UnitLabourCost);
        Assert.Contains("unitLabourCost", JsonSerializer.Serialize(lines.Items[0], JsonSerializerOptions.Web));
    }

    // ---------------------------------------------------------------- input validation surfaces from the domain

    [Fact]
    public async Task Invalid_quantity_is_rejected_before_anything_is_saved()
    {
        var estimate = InProject(Draft());
        var item = Concrete();
        _world.Catalogue[item.Id] = item;
        _world.Assign(Estimator);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _world.ServiceFor(Estimator, UserRole.Estimator).AddOrAdjustLineAsync(estimate.Id, new AddLineRequest(item.Id, 0m, 5m), Ct));
        Assert.Equal(0, _world.SaveCount);
    }

    [Fact]
    public async Task Page_size_is_bounded()
    {
        var estimate = InProject(Draft());
        _world.Assign(Estimator);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _world.ServiceFor(Estimator, UserRole.Estimator).GetLinesAsync(estimate.Id, 1, 50_000, Ct));
    }
}
