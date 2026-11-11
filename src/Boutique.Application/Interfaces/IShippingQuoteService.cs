namespace Boutique.Application.Interfaces;

public record ShippingQuoteRequest(string Pincode, bool IsCod, decimal TotalWeightKg, decimal Subtotal, decimal ItemCost);

public record ShippingQuote(
    bool Deliverable, string? Message,
    decimal Charge, decimal StandardCharge, decimal Saving, decimal AmountToFreeDelivery,
    decimal? FreeDeliveryAtSubtotal, decimal? PrepaidSaving,
    string Eta, DateOnly? DeliveryBy, decimal ChargeableWeightKg, decimal ExpectedCourierCost, string Source)
{
    public static ShippingQuote Unavailable(string message) =>
        new(false, message, 0m, 0m, 0m, 0m, null, null, "", null, 0m, 0m, "none");
}

public interface IShippingQuoteService
{
    Task<ShippingQuote> QuoteAsync(ShippingQuoteRequest request, CancellationToken ct = default);
}