namespace ConstructionEstimator.Domain.Estimates;

public enum EstimateStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}

public enum EstimateAction
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Reopened = 5,
    DescriptionCorrected = 6,
}

public enum ApprovalOutcome
{
    /// <summary>The estimate moved Submitted → Approved and one audit entry was written.</summary>
    Approved,
    /// <summary>It was already approved; nothing changed and nothing was recorded (R12).</summary>
    AlreadyApproved,
}
