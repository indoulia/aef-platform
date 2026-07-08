using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adapters.Rest;

public static class RequestObservabilityMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestObservability(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<RequestObservabilityMiddlewareMarker>>();
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                RestRequestMetrics.RecordRequest(context.Request.Path, context.Response.StatusCode, stopwatch.Elapsed);
                logger.LogInformation(
                    "Request completed: {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        });
}

// Marker type for the ILogger<T> category name only - never instantiated.
public sealed class RequestObservabilityMiddlewareMarker
{
    private RequestObservabilityMiddlewareMarker() { }
}
