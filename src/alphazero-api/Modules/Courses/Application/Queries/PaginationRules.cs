using FluentValidation;

namespace AlphaZero.Modules.Courses.Application.Queries;

/// <summary>
/// Shared boundary rules for paged queries across the module.
/// </summary>
public static class PaginationRules
{
    public const int MaxPerPage = 100;

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

    public static IRuleBuilderOptions<T, int> ValidPerPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, MaxPerPage).WithMessage($"PerPage must be between 1 and {MaxPerPage}.");
}
