namespace ConstructionEstimator.Domain.Common;

/// <summary>Base type for every rule violation raised by the domain.</summary>
public abstract class DomainException(string code, string message) : Exception(message)
{
    /// <summary>Stable, machine-readable error code (returned to clients).</summary>
    public string Code { get; } = code;
}

/// <summary>An input value is malformed or out of range (e.g. quantity ≤ 0). Maps to 400.</summary>
public sealed class DomainValidationException(string field, string message)
    : DomainException("validation_failed", message)
{
    public string Field { get; } = field;
}

/// <summary>A business rule rejects the operation in the current state (e.g. submit with no lines). Maps to 422.</summary>
public sealed class BusinessRuleViolationException(string code, string message)
    : DomainException(code, message);

/// <summary>The requested lifecycle transition is not allowed from the current status (R8). Maps to 409.</summary>
public sealed class InvalidStateTransitionException(string from, string action)
    : DomainException("invalid_transition", $"Cannot {action} an estimate that is {from}.");

/// <summary>The estimate's commercial content is locked in its current status (R10). Maps to 409.</summary>
public sealed class EstimateNotEditableException(string status)
    : DomainException("estimate_not_editable", $"Estimate lines can only be changed while the estimate is Draft (it is {status}).");

/// <summary>A reviewer tried to approve an estimate they created (R16). Maps to 403.</summary>
public sealed class SelfApprovalNotAllowedException()
    : DomainException("self_approval_not_allowed", "A reviewer may not approve an estimate they created.");
