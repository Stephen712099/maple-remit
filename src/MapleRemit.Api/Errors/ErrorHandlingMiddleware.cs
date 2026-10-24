using MapleRemit.Api.Contracts;

namespace MapleRemit.Api.Errors;

// Exceptions → JSON errors.
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException
                                   && context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to send.
        }
        // Catches apply only before the response starts.
        catch (ApiException ex) when (!context.Response.HasStarted)
        {
            await Write(context, ex.StatusCode, new ErrorResponse(ex.Code, ex.Message));
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(context, StatusCodes.Status500InternalServerError,
                new ErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred."));
        }
    }

    private static Task Write(HttpContext context, int status, ErrorResponse body)
    {
        // Drop headers set before the failure.
        context.Response.Clear();
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(body);
    }
}
