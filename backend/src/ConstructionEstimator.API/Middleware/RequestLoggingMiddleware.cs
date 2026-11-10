using System.Diagnostics;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ConstructionEstimator.API.Middleware;

/// <summary>One structured log line per request, with user and trace id in scope for every log written during it.</summary>
internal sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = context.TraceIdentifier,
            ["UserId"] = userId,
        });
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
