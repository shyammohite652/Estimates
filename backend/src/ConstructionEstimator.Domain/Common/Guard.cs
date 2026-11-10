namespace ConstructionEstimator.Domain.Common;

internal static class Guard
{
    public static string RequiredText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException(field, $"{field} is required.");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainValidationException(field, $"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new DomainValidationException(field, $"{field} is required.") : value;

    /// <summary>True when <paramref name="value"/> has no significant digits beyond <paramref name="places"/>.</summary>
    public static bool HasAtMostDecimalPlaces(decimal value, int places) =>
        decimal.Round(value, places) == value;
}
