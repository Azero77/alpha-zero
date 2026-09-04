using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Shared.Application;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationFailures = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = validationFailures
            .Where(validationResult => !validationResult.IsValid)
            .SelectMany(validationResult => validationResult.Errors)
            .FromValidationToErrors();

        if (errors.Any())
        {
            return (dynamic)errors;
        }

        return await next();
    }
}

public static class ValidationConverter
{
    public static List<Error> FromValidationToErrors(this IEnumerable<ValidationFailure> failures)
    {
        var result = new List<Error>();
        foreach (var failure in failures)
        {
            result.Add(Error.Validation(
                failure.ErrorCode,
                failure.ErrorMessage,
                new Dictionary<string, object>()
                {
                    { "PropertyName" ,failure.PropertyName},
                    { "AttemptedValue", failure.AttemptedValue }
                }));
        }

        return result;
    }
}
public static class ValidationExtensions
{
    public static void AddFluentValidation(this IServiceCollection services, Type marker)
    {
        services.AddValidatorsFromAssembly(marker.Assembly);
    }
}