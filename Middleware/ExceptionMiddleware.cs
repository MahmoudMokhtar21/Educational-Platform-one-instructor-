using Educatinal_Platform.Exceptions;
using System.Text.Json;

namespace Educatinal_Platform.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception after the response started for {Method} {Path}.", context.Request.Method, context.Request.Path.Value);
                throw;
            }

            var statusCode = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                BadRequestException => StatusCodes.Status400BadRequest,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ForbiddenException => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError
            };

            var isServerError = statusCode == StatusCodes.Status500InternalServerError;
            if (isServerError)
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path.Value);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = new
            {
                success = false,
                statusCode,
                // Known domain exceptions are user-facing; unexpected failures must
                // not disclose database, filesystem, or provider details to clients.
                message = isServerError
                    ? "An unexpected error occurred. Please try again later."
                    : ex.Message
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}