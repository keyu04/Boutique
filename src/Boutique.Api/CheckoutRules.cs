using Boutique.Domain;

namespace Boutique.Api;

public static class CheckoutRules
{
    public const int MaxLines = 20;
    public const int MaxQuantityPerLine = 10;

    // Returns an error message, or null when every quantity is acceptable.
    public static string? ValidateQuantities(IReadOnlyCollection<int> quantities)
    {
        if (quantities.Count > MaxLines)
            return $"A bag can hold at most {MaxLines} different items.";
        if (quantities.Any(q => q < 1 || q > MaxQuantityPerLine))
            return $"Quantity must be between 1 and {MaxQuantityPerLine} for each item.";
        return null;
    }

    // Accepts only the names "Prepaid" or "Cod" (any letter case).
    // "1", "99" and "Prepaid,Cod" all parse as numbers or flags, so we require
    // the parsed value's name to match the text exactly.
    public static bool TryParsePaymentMethod(string? text, out PaymentMethod method)
    {
        method = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        return Enum.TryParse(t, ignoreCase: true, out method)
               && Enum.IsDefined(method)
               && method.ToString().Equals(t, StringComparison.OrdinalIgnoreCase);
    }
}