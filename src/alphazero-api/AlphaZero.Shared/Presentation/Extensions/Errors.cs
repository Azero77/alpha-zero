using ErrorOr;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace AlphaZero.Shared.Presentation.Extensions;

/// <summary>
/// Unified error response shape used for BOTH validation and business errors.
/// Every error — regardless of origin — is serialized as:
/// {
///   "status": 400,
///   "title": "Validation.Failed",          // first error code, or a category title
///   "errors": [
///     {
///       "code":        "Email.Required",
///       "description": "Email is required",
///       "type":        2,
///       "metadata": { "propertyName": "Email", "attemptedValue": "" }
///     }
///   ]
/// }
/// </summary>
public static class ErrorExtension
{
    // -----------------------------------------------------------------------
    // FastEndpoints endpoint helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sends a unified error JSON response from a FastEndpoints endpoint.
    /// All errors (Validation, NotFound, Conflict, …) use the same shape.
    /// </summary>
    public static async Task SendErrorResponseAsync(
        this FastEndpoints.IEndpoint endpoint,
        List<Error> errors,
        CancellationToken ct = default)
    {
        if (!errors.Any())
        {
            await endpoint.HttpContext.Response.SendAsync(
                UnifiedErrorBody.Unexpected(),
                StatusCodes.Status500InternalServerError,
                cancellation: ct);
            return;
        }

        var statusCode = ResolveStatusCode(errors);
        var body = UnifiedErrorBody.From(errors, statusCode);

        await endpoint.HttpContext.Response.SendAsync(body, statusCode, cancellation: ct);
    }

    // -----------------------------------------------------------------------
    // Minimal API helper
    // -----------------------------------------------------------------------

    public static IResult ToMinimalResult(this List<Error> errors)
    {
        if (!errors.Any())
        {
            return Results.Json(UnifiedErrorBody.Unexpected(), statusCode: StatusCodes.Status500InternalServerError);
        }

        var statusCode = ResolveStatusCode(errors);
        return Results.Json(UnifiedErrorBody.From(errors, statusCode), statusCode: statusCode);
    }

    // -----------------------------------------------------------------------
    // ProblemDetails helper (kept for Swagger / middleware compatibility)
    // -----------------------------------------------------------------------

    public static ProblemDetails ToProblemDetails(this List<Error> errors)
    {
        var statusCode = errors.Count == 1
            ? MapSingleErrorStatus(errors[0].Type)
            : StatusCodes.Status400BadRequest;

        return new ProblemDetails
        {
            Status = statusCode,
            Title = errors.FirstOrDefault().Code,
            Extensions = new Dictionary<string, object?>
            {
                { "errors", errors.Select(ErrorDto.From).ToList() }
            }
        };
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private static int ResolveStatusCode(List<Error> errors)
    {
        // Multiple errors → always 400 (typically all are validation)
        if (errors.Count > 1)
            return StatusCodes.Status400BadRequest;

        return MapSingleErrorStatus(errors[0].Type);
    }

    private static int MapSingleErrorStatus(ErrorType type) => type switch
    {
        ErrorType.Failure     => StatusCodes.Status400BadRequest,
        ErrorType.Unexpected  => StatusCodes.Status500InternalServerError,
        ErrorType.Validation  => StatusCodes.Status400BadRequest,
        ErrorType.Conflict    => StatusCodes.Status409Conflict,
        ErrorType.NotFound    => StatusCodes.Status404NotFound,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden   => StatusCodes.Status403Forbidden,
        _                     => StatusCodes.Status400BadRequest
    };
}

// ---------------------------------------------------------------------------
// Shared response body factory
// ---------------------------------------------------------------------------

internal static class UnifiedErrorBody
{
    internal static object From(List<Error> errors, int statusCode) => new
    {
        status = statusCode,
        title  = errors.Count == 1 ? errors[0].Code : "Request.Failed",
        errors = errors.Select(ErrorDto.From).ToList()
    };

    internal static object Unexpected() => new
    {
        status = 500,
        title  = "General.Unexpected",
        errors = new[]
        {
            new { code = "General.Unexpected", description = "An unexpected error occurred.", type = "Unexpected", metadata = (object?)null }
        }
    };
}

// ---------------------------------------------------------------------------
// DTO used in every error array item
// ---------------------------------------------------------------------------

public sealed record ErrorDto(
    string Code,
    string Description,
    string Type,          // "Validation", "NotFound", "Conflict", etc.
    object? Metadata)
{
    public static ErrorDto From(Error e) => new(
        e.Code,
        e.Description,
        e.Type.ToString(), // maps ErrorType enum name directly
        e.Metadata?.Count > 0 ? e.Metadata : null);
}
