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
        StatusCodes.Status415UnsupportedMediaType => new(ErrorCodes.UnsupportedMediaType, "Content-Type must be application/json."),
        >= StatusCodes.Status500InternalServerError => new(ErrorCodes.InternalError, "An unexpected error occurred."),
        _ => new(ErrorCodes.InvalidRequest, "The request is invalid."),
    };

    // JSON body for framework errors (404, 405, 415).
    public static Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        return response.WriteAsJsonAsync(For(response.StatusCode));
    }
}
