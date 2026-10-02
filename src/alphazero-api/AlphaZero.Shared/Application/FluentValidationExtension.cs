using FluentValidation;

namespace AlphaZero.Shared.Application;

public static class FluentValidationExtension
{
    public static void IsValidUrl<TRequest>(this IRuleBuilder<TRequest, string> ruleBuilder)
    {
        // TODO
        //here after we set our s3 url , it should points to it , 
        ruleBuilder.Must(url => Uri.TryCreate(url, UriKind.Absolute, out var outUri) && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps));
    }
}