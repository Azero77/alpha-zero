using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.ValueObjects;

public record RichText
{
    public string Value { get; init; }

    private RichText(string value)
    {
        Value = value;
    }

    public static ErrorOr<RichText> Create(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Error.Validation("RichText.Empty", "Rich text content cannot be empty.");

        // The Application/Presentation layer should handle deep JSON validation 
        // to avoid leaking JSON dependencies into the Domain model.
        return new RichText(content);
    }
}
