using System.Diagnostics;
using MapleRemit.Api.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace MapleRemit.Api.Errors;

public static class StatusCodeErrors
{
    // Single status → error-code mapping.
    public static ErrorResponse For(int status) => status switch
    {
        StatusCodes.Status400BadRequest => new(ErrorCodes.InvalidRequest, "The request is malformed."),
        StatusCodes.Status404NotFound => new(ErrorCodes.NotFound, "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => new(ErrorCodes.MethodNotAllowed, "The HTTP method is not allowed for this resource."),
        StatusCodes.Status413PayloadTooLarge => new(ErrorCodes.PayloadTooLarge, "The request body is too large."),
        StatusCodes.Status415UnsupportedMediaType => new(ErrorCodes.UnsupportedMediaType, "Content-Type must be application/json."),
        >= StatusCodes.Status500InternalServerError => new(ErrorCodes.InternalError, "An unexpected error occurred."),
        _ => new(ErrorCodes.InvalidRequest, "The request is invalid."),
    };

    // W3C trace ID of the current request; falls back to the ASP.NET Core request ID.
    public static ErrorResponse WithTraceId(this ErrorResponse body, HttpContext context) =>
        body with { TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier };

    // Info, not error: client mistakes should not page anyone. Count them by ErrorCode.
    public static void LogClientError(ILogger logger, int status, string code) =>
        logger.LogInformation("Client error {StatusCode} {ErrorCode}", status, code);

    // JSON body for framework errors (404, 405, 415).
    public static Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var body = For(response.StatusCode);
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(StatusCodeErrors));
        LogClientError(logger, response.StatusCode, body.Code);
        return response.WriteAsJsonAsync(body.WithTraceId(context.HttpContext));
    }
}
