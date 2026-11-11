namespace Boutique.Application.Interfaces;

public record CourierQuote(decimal Rate, decimal CodCharges, int EstimatedDays, bool SupportsCod);

public interface ICourierRateProvider
{
    Task<IReadOnlyList<CourierQuote>?> GetQuotesAsync(
        string pickupPincode,
        string deliveryPincode,
        decimal weightKg,
        bool cod,
        CancellationToken ct = default);
}
