using Boutique.Application.Interfaces;
using Boutique.Application.Shipping;
using Microsoft.Extensions.Options;

namespace Boutique.Infrastructure.Shipping;

public class ShippingQuoteService : IShippingQuoteService
{
    private readonly ICourierRateProvider _couriers;
    private readonly IShippingService _fallback;          // the old rate table, used when Shiprocket cannot answer
    private readonly ShiprocketOptions _sr;
    private readonly ShippingPolicyOptions _policy;

    public ShippingQuoteService(
        ICourierRateProvider couriers, IShippingService fallback,
        IOptions<ShiprocketOptions> sr, IOptions<ShippingPolicyOptions> policy)
    {
        _couriers = couriers;
        _fallback = fallback;
        _sr = sr.Value;
        _policy = policy.Value;
    }

    private sealed record Priced(
        ShippingPolicyResult Policy, string Eta, DateOnly? DeliveryBy,
        decimal ChargeableKg, decimal CourierCost, string Source);

    public async Task<ShippingQuote> QuoteAsync(ShippingQuoteRequest req, CancellationToken ct = default)
    {
        var (main, message) = await PriceAsync(req, req.IsCod, ct);
        if (main is null) return ShippingQuote.Unavailable(message ?? "We can't deliver to this pincode yet.");

        // For cash on delivery, also price it as prepaid so the customer can see exactly what paying online saves.
        decimal? prepaidSaving = null;
        if (req.IsCod)
        {
            var (prepaid, _) = await PriceAsync(req, false, ct);
            if (prepaid is not null)
                prepaidSaving = Math.Max(0m, main.Policy.Charge - prepaid.Policy.Charge);
        }

        return new ShippingQuote(
            true, null,
            main.Policy.Charge, main.Policy.StandardCharge, main.Policy.Saving, main.Policy.AmountToFreeDelivery,
            main.Policy.FreeDeliveryAtSubtotal, prepaidSaving,
            main.Eta, main.DeliveryBy, main.ChargeableKg, main.CourierCost, main.Source);
    }

    private async Task<(Priced? Priced, string? Message)> PriceAsync(ShippingQuoteRequest req, bool cod, CancellationToken ct)
    {
        var live = await _couriers.GetQuotesAsync(
            _sr.PickupPincode, req.Pincode, req.TotalWeightKg + _policy.PackagingWeightKg, cod, ct);

        decimal courierCost;
        string eta;
        DateOnly? deliveryBy = null;
        decimal chargeableKg;
        string source;

        if (live is null)
        {
            // Shiprocket unavailable: use the table so checkout keeps working.
            var (charge, tableEta, chargeable) = _fallback.Estimate(req.TotalWeightKg, req.Pincode, cod ? "Cod" : "Prepaid");
            courierCost = charge;
            eta = tableEta;
            chargeableKg = chargeable;
            source = "table";
        }
        else
        {
            if (live.Count == 0) return (null, "Sorry, we can't deliver to this pincode yet.");

            var best = live
                .Where(q => !cod || q.SupportsCod)
                .OrderBy(q => q.Rate + (cod ? q.CodCharges : 0m))   // cheapest, not Shiprocket's "recommended"
                .ThenBy(q => q.EstimatedDays)
                .FirstOrDefault();
            if (best is null)
                return (null, "Cash on delivery isn't available for this pincode. Please choose online payment.");

            var days = best.EstimatedDays + _policy.HandlingDays;
            courierCost = best.Rate + (cod ? best.CodCharges : 0m);
            deliveryBy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(days);   // India time, no DST
            eta = $"{days}–{days + 1} days";
            chargeableKg = Math.Max(0.5m, Math.Ceiling((req.TotalWeightKg + _policy.PackagingWeightKg) * 2) / 2m);
            source = "shiprocket";
        }

        var policy = ShippingPolicy.Calculate(
            new ShippingPolicyInput(req.Subtotal, req.ItemCost, courierCost, cod), _policy);
        return (new Priced(policy, eta, deliveryBy, chargeableKg, courierCost, source), null);
    }
}