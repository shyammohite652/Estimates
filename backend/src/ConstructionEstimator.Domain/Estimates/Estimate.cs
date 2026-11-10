using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.Domain.Estimates;

/// <summary>
/// Aggregate root for an estimate's status, header and history.
///
/// Lines are deliberately NOT held as a loaded collection: an estimate can carry 50 000 lines (R19),
/// and loading them all to change one would not scale. Every line mutation still goes through a
/// method on this class so the Draft-only rule (R10) cannot be bypassed. Invariants that need all
/// lines are passed in (line count for R9) or enforced by the database (R7 uniqueness, R5 sum).
/// </summary>
public sealed class Estimate
{
    public const int DescriptionMaxLength = 500;
    public const int ReasonMaxLength = 1000;

    private readonly List<EstimateHistoryEntry> _history = [];

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public DateOnly PricingDate { get; private set; }
    public EstimateStatus Status { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Touched on every change (including line changes) so the row's concurrency token moves.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<EstimateHistoryEntry> History => _history;

    private Estimate() { }

    public static Estimate Create(Guid id, Guid projectId, string number, string description,
        DateOnly pricingDate, Guid createdById, DateTimeOffset now)
    {
        var estimate = new Estimate
        {
            Id = Guard.NotEmpty(id, nameof(id)),
            ProjectId = Guard.NotEmpty(projectId, nameof(projectId)),
            Number = Guard.RequiredText(number, nameof(number), 32),
            Description = Guard.RequiredText(description, "description", DescriptionMaxLength),
            PricingDate = pricingDate,
            Status = EstimateStatus.Draft,
            CreatedById = Guard.NotEmpty(createdById, nameof(createdById)),
            CreatedAt = now,
            UpdatedAt = now,
        };
        estimate.Record(EstimateAction.Created, null, EstimateStatus.Draft, createdById, now, null);
        return estimate;
    }

    // ---------------------------------------------------------------- lines (R2, R4, R7, R10)

    /// <summary>
    /// R7: adds <paramref name="item"/> or, if the estimate already has a line for it
    /// (<paramref name="existingLine"/>), adjusts that line — replacing its quantity and markup
    /// (DESIGN.md A5). The caller looks the existing line up; the database's unique index
    /// (EstimateId, CatalogueItemId) is the backstop for races.
    /// </summary>
    public EstimateLine AddOrAdjustLine(CatalogueItem item, EstimateLine? existingLine,
        Quantity quantity, MarkupPercentage markup, DateTimeOffset now)
    {
        EnsureLinesEditable();
        if (existingLine is not null)
        {
            EnsureOwns(existingLine);
            if (existingLine.CatalogueItemId != item.Id)
                throw new InvalidOperationException("Existing line is for a different catalogue item.");
            existingLine.Reprice(quantity, markup);
            Touch(now);
            return existingLine;
        }

        var rate = item.RateEffectiveOn(PricingDate); // R2: pricing date, not today
        var line = new EstimateLine(Id, item, rate, quantity, markup);
        Touch(now);
        return line;
    }

    public void ChangeLine(EstimateLine line, Quantity quantity, MarkupPercentage markup, DateTimeOffset now)
    {
        EnsureLinesEditable();
        EnsureOwns(line);
        line.Reprice(quantity, markup);
        Touch(now);
    }

    /// <summary>Validates that <paramref name="line"/> may be removed; the caller deletes it.</summary>
    public void RemoveLine(EstimateLine line, DateTimeOffset now)
    {
        EnsureLinesEditable();
        EnsureOwns(line);
        Touch(now);
    }

    // ---------------------------------------------------------------- lifecycle (R8, R9, R12, R13, R16)

    /// <summary>R9: needs at least one line and a pricing date that is not in the future.</summary>
    public void Submit(int lineCount, DateOnly today, Guid userId, DateTimeOffset now)
    {
        if (Status != EstimateStatus.Draft)
            throw new InvalidStateTransitionException(Status.ToString(), "submit");
        if (lineCount < 1)
            throw new BusinessRuleViolationException("no_lines", "An estimate needs at least one line before it can be submitted.");
        if (PricingDate > today)
            throw new BusinessRuleViolationException("pricing_date_in_future",
                $"The pricing date {PricingDate:yyyy-MM-dd} is in the future; it must be on or before {today:yyyy-MM-dd} to submit.");
        Transition(EstimateAction.Submitted, EstimateStatus.Submitted, userId, now, null);
    }

    /// <summary>
    /// R12: idempotent. Approving an already-approved estimate changes nothing and records nothing.
    /// R16: checked against who created the estimate, not against the approver's current role.
    /// </summary>
    public ApprovalOutcome Approve(Guid reviewerId, DateTimeOffset now)
    {
        if (Status == EstimateStatus.Approved)
            return ApprovalOutcome.AlreadyApproved;
        if (Status != EstimateStatus.Submitted)
            throw new InvalidStateTransitionException(Status.ToString(), "approve");
        if (reviewerId == CreatedById)
            throw new SelfApprovalNotAllowedException();
        Transition(EstimateAction.Approved, EstimateStatus.Approved, reviewerId, now, null);
        return ApprovalOutcome.Approved;
    }

    public void Reject(Guid reviewerId, string reason, DateTimeOffset now)
    {
        if (Status != EstimateStatus.Submitted)
            throw new InvalidStateTransitionException(Status.ToString(), "reject");
        var cleanReason = Guard.RequiredText(reason, "reason", ReasonMaxLength);
        Transition(EstimateAction.Rejected, EstimateStatus.Rejected, reviewerId, now, cleanReason);
    }

    /// <summary>Rejected → Draft, so the estimator can rework it (R8).</summary>
    public void Reopen(Guid userId, DateTimeOffset now)
    {
        if (Status != EstimateStatus.Rejected)
            throw new InvalidStateTransitionException(Status.ToString(), "reopen");
        Transition(EstimateAction.Reopened, EstimateStatus.Draft, userId, now, null);
    }

    /// <summary>
    /// R11: a description correction is allowed in any status, including Approved. It is not a
    /// lifecycle transition and never touches money or status, so it does not breach R10's intent
    /// (DESIGN.md A1). The old and new text are audited. Who may call it is an application rule.
    /// </summary>
    public bool CorrectDescription(string newDescription, Guid userId, DateTimeOffset now)
    {
        var clean = Guard.RequiredText(newDescription, "description", DescriptionMaxLength);
        if (clean == Description) return false;
        var comment = $"\"{Description}\" → \"{clean}\"";
        Description = clean;
        Touch(now);
        Record(EstimateAction.DescriptionCorrected, Status, Status, userId, now, comment);
        return true;
    }

    public bool IsLinesEditable => Status == EstimateStatus.Draft;

    // ---------------------------------------------------------------- internals

    private void EnsureLinesEditable()
    {
        if (!IsLinesEditable)
            throw new EstimateNotEditableException(Status.ToString());
    }

    private void EnsureOwns(EstimateLine line)
    {
        if (line.EstimateId != Id)
            throw new InvalidOperationException("Line does not belong to this estimate.");
    }

    private void Transition(EstimateAction action, EstimateStatus to, Guid userId, DateTimeOffset now, string? comment)
    {
        var from = Status;
        Status = to;
        Touch(now);
        Record(action, from, to, userId, now, comment);
    }

    private void Record(EstimateAction action, EstimateStatus? from, EstimateStatus? to, Guid userId,
        DateTimeOffset now, string? comment)
    {
        Guard.NotEmpty(userId, nameof(userId));
        _history.Add(new EstimateHistoryEntry(Id, action, from, to, userId, now, comment));
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
