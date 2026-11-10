using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Estimates;
using static ConstructionEstimator.UnitTests.Domain.TestData;

namespace ConstructionEstimator.UnitTests.Domain;

/// <summary>R8, R9, R11, R12, R13, R16 — the lifecycle state machine.</summary>
public class EstimateLifecycleTests
{
    [Fact]
    public void Submission_requires_at_least_one_line()
    {
        var ex = Assert.Throws<BusinessRuleViolationException>(() => Draft().Submit(0, Today, Estimator, Now));
        Assert.Equal("no_lines", ex.Code);
    }

    [Fact]
    public void Submission_requires_a_pricing_date_that_is_not_in_the_future()
    {
        var estimate = WithOneLine(pricingDate: Today.AddDays(1));
        var ex = Assert.Throws<BusinessRuleViolationException>(() => estimate.Submit(1, Today, Estimator, Now));
        Assert.Equal("pricing_date_in_future", ex.Code);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
    }

    [Fact]
    public void Pricing_date_of_today_can_be_submitted()
    {
        var estimate = WithOneLine(pricingDate: Today);
        estimate.Submit(1, Today, Estimator, Now);
        Assert.Equal(EstimateStatus.Submitted, estimate.Status);
    }

    [Fact]
    public void Happy_path_records_every_transition_with_who_and_when()
    {
        var estimate = WithOneLine();
        var submittedAt = Now.AddHours(1);
        var approvedAt = Now.AddHours(2);
        estimate.Submit(1, Today, Estimator, submittedAt);
        estimate.Approve(Reviewer, approvedAt);

        var history = estimate.History.ToList();
        Assert.Collection(history,
            h => Assert.Equal((EstimateAction.Created, Estimator, Now), (h.Action, h.UserId, h.OccurredAt)),
            h =>
            {
                Assert.Equal((EstimateAction.Submitted, Estimator, submittedAt), (h.Action, h.UserId, h.OccurredAt));
                Assert.Equal((EstimateStatus.Draft, EstimateStatus.Submitted), (h.FromStatus, h.ToStatus));
            },
            h =>
            {
                Assert.Equal((EstimateAction.Approved, Reviewer, approvedAt), (h.Action, h.UserId, h.OccurredAt));
                Assert.Equal((EstimateStatus.Submitted, EstimateStatus.Approved), (h.FromStatus, h.ToStatus));
            });
    }

    [Fact]
    public void Rejected_estimate_can_return_to_draft_and_be_resubmitted()
    {
        var estimate = Submitted();
        estimate.Reject(Reviewer, "Check quantities", Now);
        estimate.Reopen(Estimator, Now);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        estimate.Submit(1, Today, Estimator, Now);
        Assert.Equal(EstimateStatus.Submitted, estimate.Status);
        Assert.Equal("Check quantities", estimate.History.Single(h => h.Action == EstimateAction.Rejected).Comment);
    }

    public static TheoryData<string, Action<Estimate>> InvalidTransitions => new()
    {
        { "approve Draft", e => e.Approve(Reviewer, Now) },
        { "reject Draft", e => e.Reject(Reviewer, "x", Now) },
        { "reopen Draft", e => e.Reopen(Estimator, Now) },
        { "submit Submitted", e => { e.Submit(1, Today, Estimator, Now); e.Submit(1, Today, Estimator, Now); } },
        { "reopen Submitted", e => { e.Submit(1, Today, Estimator, Now); e.Reopen(Estimator, Now); } },
        { "reject Approved", e => { e.Submit(1, Today, Estimator, Now); e.Approve(Reviewer, Now); e.Reject(Reviewer, "x", Now); } },
        { "reopen Approved", e => { e.Submit(1, Today, Estimator, Now); e.Approve(Reviewer, Now); e.Reopen(Estimator, Now); } },
        { "submit Approved", e => { e.Submit(1, Today, Estimator, Now); e.Approve(Reviewer, Now); e.Submit(1, Today, Estimator, Now); } },
        { "approve Rejected", e => { e.Submit(1, Today, Estimator, Now); e.Reject(Reviewer, "x", Now); e.Approve(Reviewer, Now); } },
        { "submit Rejected", e => { e.Submit(1, Today, Estimator, Now); e.Reject(Reviewer, "x", Now); e.Submit(1, Today, Estimator, Now); } },
    };

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Only_the_transitions_in_R8_are_allowed(string _, Action<Estimate> act)
    {
        Assert.Throws<InvalidStateTransitionException>(() => act(WithOneLine()));
    }

    [Fact]
    public void Approval_is_idempotent_and_writes_a_single_audit_entry()
    {
        var estimate = Submitted();

        var first = estimate.Approve(Reviewer, Now);
        var second = estimate.Approve(Reviewer, Now.AddMinutes(1));

        Assert.Equal(ApprovalOutcome.Approved, first);
        Assert.Equal(ApprovalOutcome.AlreadyApproved, second);
        Assert.Single(estimate.History, h => h.Action == EstimateAction.Approved);
    }

    [Fact]
    public void A_reviewer_cannot_approve_an_estimate_they_created()
    {
        // e.g. created while they held the Estimator role (DESIGN.md A2)
        var estimate = Submitted(createdBy: Reviewer);
        Assert.Throws<SelfApprovalNotAllowedException>(() => estimate.Approve(Reviewer, Now));
        Assert.Equal(EstimateStatus.Submitted, estimate.Status);
    }

    [Fact]
    public void A_reviewer_may_reject_an_estimate_they_created()
    {
        var estimate = Submitted(createdBy: Reviewer);
        estimate.Reject(Reviewer, "Withdrawing my own estimate", Now);
        Assert.Equal(EstimateStatus.Rejected, estimate.Status);
    }

    [Fact]
    public void Description_of_an_approved_estimate_can_be_corrected_without_changing_status_and_is_audited()
    {
        var estimate = Approved();

        var changed = estimate.CorrectDescription("Test estimate (corrected)", Manager, Now);

        Assert.True(changed);
        Assert.Equal(EstimateStatus.Approved, estimate.Status);
        Assert.Equal("Test estimate (corrected)", estimate.Description);
        var entry = estimate.History.Last();
        Assert.Equal((EstimateAction.DescriptionCorrected, Manager), (entry.Action, entry.UserId));
        Assert.Contains("Test estimate", entry.Comment);
    }

    [Fact]
    public void Rejecting_requires_a_reason()
    {
        Assert.Throws<DomainValidationException>(() => Submitted().Reject(Reviewer, "  ", Now));
    }
}
