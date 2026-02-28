using System.Diagnostics;
using System.Security.Claims;

namespace SqlFroega.Api.Infrastructure;

internal sealed class CorrelationAndAuditMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationAndAuditMiddleware> _logger;

    public CorrelationAndAuditMiddleware(RequestDelegate next, ILogger<CorrelationAndAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationHeader, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Activity.Current?.Id ?? context.TraceIdentifier;

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        var started = DateTime.UtcNow;
        await _next(context);

        var user = context.User?.Identity?.Name ?? "anonymous";
        var scopes = string.Join(',', context.User?.Claims.Where(c => c.Type == "scope").Select(c => c.Value) ?? Array.Empty<string>());
        var tenant = context.Request.Headers["X-Tenant-Context"].ToString();

        _logger.LogInformation(
            "AUDIT {Method} {Path} => {StatusCode}; User={User}; Scopes={Scopes}; Tenant={Tenant}; CorrelationId={CorrelationId}; Timestamp={TimestampUtc}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            user,
            string.IsNullOrWhiteSpace(scopes) ? "none" : scopes,
            string.IsNullOrWhiteSpace(tenant) ? "none" : tenant,
            correlationId,
            started);
    }
}

internal static class CorrelationAndAuditMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationAndAudit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationAndAuditMiddleware>();
    }
}
