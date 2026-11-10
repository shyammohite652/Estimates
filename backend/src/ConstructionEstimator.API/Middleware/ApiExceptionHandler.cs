using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ConstructionEstimator.API.Middleware;

/// <summary>
/// The single place where exceptions become HTTP responses (RFC 7807 ProblemDetails).
/// Controllers contain no try/catch.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code, errors) = exception switch
        {
            DomainValidationException e => (400, "Validation failed", e.Code, Errors(e.Field, e.Message)),
            RequestValidationException e => (400, "Validation failed", "validation_failed", Errors(e.Field, e.Message)),
            BadHttpRequestException => (400, "Malformed request", "bad_request", null),
            NotFoundException => (404, "Not found", "not_found", null),
            ForbiddenException => (403, "Forbidden", "forbidden", null),
            SelfApprovalNotAllowedException e => (403, "Forbidden", e.Code, null),
            UnauthorizedAccessException => (401, "Unauthorized", "unauthorized", null),
            InvalidStateTransitionException e => (409, "Invalid state transition", e.Code, null),
            EstimateNotEditableException e => (409, "Estimate is locked", e.Code, null),
            ConflictException => (409, "Conflict", "conflict", null),
            BusinessRuleViolationException e => (422, "Business rule violated", e.Code, null),
            OperationCanceledException when ct.IsCancellationRequested => (499, "Client closed request", "cancelled", null),
            _ => (500, "Unexpected error", "server_error", (Dictionary<string, string[]>?)null),
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("Request rejected {Status} {Code}: {Message}", status, code, exception.Message);

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Never leak internals on 500s.
            Detail = status >= 500 ? "An unexpected error occurred. Quote the trace id when reporting it." : exception.Message,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is not null) problem.Extensions["errors"] = errors;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static Dictionary<string, string[]> Errors(string field, string message) => new() { [field] = [message] };
}
