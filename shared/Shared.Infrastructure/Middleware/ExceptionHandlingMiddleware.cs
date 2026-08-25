using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace Shared.Infrastructure.Middleware;

/// <summary>
/// Catches every unhandled exception and maps it to the standard ApiResponse envelope, so no
/// controller needs its own try/catch for the common cases. Never leaks a stack trace to the
/// client in production.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var message = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage));
            await WriteAsync(context, HttpStatusCode.BadRequest, ErrorCodes.ValidationError, message);
        }
        catch (AuthenticationFailedException ex)
        {
            await WriteAsync(context, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteAsync(context, HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, HttpStatusCode.NotFound, ErrorCodes.NotFound, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteAsync(context, HttpStatusCode.NotFound, ErrorCodes.NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(context, HttpStatusCode.Conflict, ErrorCodes.Conflict, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path} [{CorrelationId}]",
                context.Request.Method, context.Request.Path, context.GetCorrelationId());

            var message = environment.IsProduction()
                ? "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."
                : ex.ToString();
            await WriteAsync(context, HttpStatusCode.InternalServerError, ErrorCodes.InternalError, message);
        }
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode status, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";

        var body = ApiResponse.Fail(code, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
