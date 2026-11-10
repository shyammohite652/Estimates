using System.Text.Json.Serialization;
using ConstructionEstimator.Domain.Estimates;

namespace ConstructionEstimator.Application.Estimates;

/// <summary>Header row as read from the database, including server-computed totals (R5).</summary>
public sealed record EstimateHeaderData(
    Guid Id, Guid ProjectId, string ProjectName, string Number, string Description,
    DateOnly PricingDate, EstimateStatus Status, Guid CreatedById, string CreatedByName,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int LineCount, decimal NetTotal, decimal MarkupTotal, decimal Total, decimal? LabourTotal);

/// <summary>What the caller may do with this estimate right now. Computed on the server; the UI only reflects it.</summary>
public sealed record EstimateCapabilities(
    bool CanEditLines, bool CanSubmit, bool CanApprove, bool CanReject, bool CanReopen,
    bool CanCorrectDescription, bool CanViewLabourCost);

public sealed record EstimateDetailsDto(
    Guid Id, Guid ProjectId, string ProjectName, string Number, string Description,
    DateOnly PricingDate, EstimateStatus Status, Guid CreatedById, string CreatedByName,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int LineCount, decimal NetTotal, decimal MarkupTotal, decimal Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? LabourTotal,
    EstimateCapabilities Capabilities);

public sealed record EstimateLineDto(
    long Id, Guid CatalogueItemId, string ItemCode, string ItemDescription, string Unit,
    decimal Quantity, decimal MarkupPercent, decimal UnitRate, DateOnly RateEffectiveFrom,
    decimal NetAmount, decimal MarkupAmount, decimal LineTotal,
    // R17: omitted from the JSON entirely for roles that may not see labour cost.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? UnitLabourCost,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? LabourAmount);

public sealed record EstimateHistoryDto(
    long Id, EstimateAction Action, EstimateStatus? FromStatus, EstimateStatus? ToStatus,
    Guid UserId, string UserName, DateTimeOffset OccurredAt, string? Comment);

public sealed record LineMutationResult(EstimateLineDto Line, bool Created);

// ---- requests

public sealed record CreateEstimateRequest(string Description, DateOnly PricingDate);
public sealed record AddLineRequest(Guid CatalogueItemId, decimal Quantity, decimal MarkupPercent);
public sealed record UpdateLineRequest(decimal Quantity, decimal MarkupPercent);
public sealed record RejectEstimateRequest(string Reason);
public sealed record CorrectDescriptionRequest(string Description);
