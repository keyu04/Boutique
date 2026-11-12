namespace Boutique.Application.Shipping;

public static class ShippingTolerance
{
    public const decimal Rupees = 1m;
    public static bool Matches(decimal? expected, decimal actual) =>
        expected is null || Math.Abs(expected.Value - actual) <= Rupees;
}