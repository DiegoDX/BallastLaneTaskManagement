using System.Net;
using System.Text.Json;
using Api.Models;
using Application.Exceptions;

namespace Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = MapException(exception);

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ErrorResponse
        {
            StatusCode = statusCode,
            Message = message
        };

        await context.Response
            .WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int StatusCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException =>
                ((int)HttpStatusCode.BadRequest, validationException.Message),

            NotFoundException notFoundException =>
                ((int)HttpStatusCode.NotFound, notFoundException.Message),

            UnauthorizedException unauthorizedException =>
                ((int)HttpStatusCode.Unauthorized, unauthorizedException.Message),

            UnauthorizedAccessException unauthorizedAccessException =>
                ((int)HttpStatusCode.Unauthorized, unauthorizedAccessException.Message),

            LlmException { IsTransient: true } =>
                ((int)HttpStatusCode.ServiceUnavailable, "The LLM service is temporarily unavailable."),

            LlmException llmException =>
                ((int)HttpStatusCode.BadGateway, llmException.Message),

            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };
    }
}
