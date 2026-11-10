namespace ConstructionEstimator.Application.Common;

/// <summary>The resource does not exist, or the caller may not know it exists (R18). Maps to 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>The caller is known but lacks the permission for this action. Maps to 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>A concurrent change or a uniqueness constraint won the race. Maps to 409.</summary>
public sealed class ConflictException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Request shape is invalid (paging, missing ids). Maps to 400.</summary>
public sealed class RequestValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
