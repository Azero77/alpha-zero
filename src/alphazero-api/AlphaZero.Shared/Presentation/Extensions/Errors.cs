using ErrorOr;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using FastEndpoints;
using System.Net;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace AlphaZero.Shared.Presentation.Extensions;

public static class ErrorExtension
{
    public static ProblemDetails ToProblemDetails(this List<Error> errors)
    {
        return new ProblemDetails()
        {
            Status = errors.Count switch
            {
                1 => errors.First().Type switch
                {
                    ErrorType.Failure => StatusCodes.Status400BadRequest,
                    ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
                    ErrorType.Validation => StatusCodes.Status400BadRequest,
                    ErrorType.Conflict => StatusCodes.Status409Conflict,
                    ErrorType.NotFound => StatusCodes.Status404NotFound,
                    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                },
                _ => StatusCodes.Status400BadRequest
            },
            Extensions = new Dictionary<string, object?>
            {
                {"Errors", errors}
            }
        };
    }

    public static async Task SendErrorResponseAsync(this FastEndpoints.IEndpoint endpoint, List<Error> errors, CancellationToken ct = default)
    {
        if (!errors.Any())
        {
            await endpoint.HttpContext.Response.SendAsync(new { Title = "An unexpected error occurred.", Status = 500 }, 500, cancellation: ct);
            return;
        }

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var failures = errors.Select(e => new ValidationFailure(e.Code, e.Description)).ToList();
            await endpoint.HttpContext.Response.SendErrorsAsync(failures, (int)HttpStatusCode.BadRequest, cancellation: ct);
            return;
        }

        var firstError = errors[0];
        var statusCode = firstError.Type switch
        {
            ErrorType.Conflict => (int)HttpStatusCode.Conflict,
            ErrorType.Validation => (int)HttpStatusCode.BadRequest,
            ErrorType.NotFound => (int)HttpStatusCode.NotFound,
            ErrorType.Unauthorized => (int)HttpStatusCode.Unauthorized,
            ErrorType.Forbidden => (int)HttpStatusCode.Forbidden,
            _ => (int)HttpStatusCode.InternalServerError
        };

        await endpoint.HttpContext.Response.SendAsync(new
        {
            Title = firstError.Code,
            Detail = firstError.Description,
            Status = statusCode,
            Errors = errors
        }, statusCode, cancellation: ct);
    }

    public static IResult ToMinimalResult(this List<Error> errors)
    {
        if (!errors.Any())
        {
            return Results.Problem(statusCode: (int)HttpStatusCode.InternalServerError, title: "An unexpected error occurred.");
        }

        var firstError = errors[0];
        var statusCode = firstError.Type switch
        {
            ErrorType.Conflict => (int)HttpStatusCode.Conflict,
            ErrorType.Validation => (int)HttpStatusCode.BadRequest,
            ErrorType.NotFound => (int)HttpStatusCode.NotFound,
            ErrorType.Unauthorized => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        return Results.Problem(statusCode: statusCode, title: firstError.Code, extensions: new Dictionary<string, object?>()
        {
            {"errors", errors}
        });
    }
}
