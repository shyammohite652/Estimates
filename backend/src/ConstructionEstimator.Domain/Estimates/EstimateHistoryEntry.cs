namespace ConstructionEstimator.Domain.Estimates;

/// <summary>R13: an immutable record of who did what to an estimate, and when.</summary>
public sealed class EstimateHistoryEntry
{
    public long Id { get; private set; }
    public Guid EstimateId { get; private set; }
    public EstimateAction Action { get; private set; }
    public EstimateStatus? FromStatus { get; private set; }
    public EstimateStatus? ToStatus { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Comment { get; private set; }

    private EstimateHistoryEntry() { }

    internal EstimateHistoryEntry(Guid estimateId, EstimateAction action, EstimateStatus? from, EstimateStatus? to,
        Guid userId, DateTimeOffset occurredAt, string? comment)
    {
        EstimateId = estimateId;
        Action = action;
        FromStatus = from;
        ToStatus = to;
        UserId = userId;
        OccurredAt = occurredAt;
        Comment = comment;
    }
}
