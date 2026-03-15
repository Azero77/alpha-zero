using ErrorOr;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

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
                {"Errors",errors}
            }
        };
    }

    public static ProblemDetails ToProblemDetails(this List<ValidationFailure> errors)
    {
        return new ProblemDetails()
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Extensions = new Dictionary<string, object?>
            {
                {"Errors",errors}
            }
        };
    }

    public static IActionResult ToProblemResult(this ProblemDetails problemDetails, HttpContext httpContext)
    {
        if (problemDetails.Status is not null)
        {
            httpContext.Response.StatusCode = problemDetails.Status.Value;
        }

        IProblemDetailsService service = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        service.WriteAsync(new ProblemDetailsContext()
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });

        return new EmptyResult();
    }

    /// <summary>
    /// For minimal apis
    /// </summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static IResult ToMinimalResult(this List<Error> errors)
    {
        if (!errors.Any())
        {
            // This should not happen
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
            {"errors",errors}
        });

    }
}