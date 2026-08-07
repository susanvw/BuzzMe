using System.Net;
using System.Text.Json;
using BuzzMe.Contracts.V1.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuzzMe.Api.Middleware;

/// <summary>
/// The single place an unhandled exception becomes `500 SERVER_ERROR`
/// (DEVELOPMENT_GUIDE.md §9, API_CONTRACT.md §6). Every *expected* business outcome is a
/// Result failure mapped explicitly by the endpoint that produced it — this middleware
/// only ever sees genuine faults.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, JsonSerializerOptions jsonOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var body = new ApiResponse<object>(
                Data: null,
                Error: new ApiError(ErrorCode.ServerError, "Something went wrong on our end."));

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, jsonOptions));
        }
    }
}
