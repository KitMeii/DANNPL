using Microsoft.AspNetCore.Http;
using Serilog.Context;
using Shared.Contracts;

namespace Shared.Infrastructure.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the incoming request (set by the gateway), generating one if
/// absent (e.g. a service called directly in dev). Pushes it onto Serilog's LogContext so every
/// log line emitted while handling the request carries it, and echoes it back on the response.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Items[HeaderNames.CorrelationId] = correlationId;
        context.Response.Headers[HeaderNames.CorrelationId] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdHttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(HeaderNames.CorrelationId, out var value) && value is string id
            ? id
            : string.Empty;
}
