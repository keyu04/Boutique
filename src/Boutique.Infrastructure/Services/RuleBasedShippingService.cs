using Boutique.Application.Interfaces;

namespace Boutique.Infrastructure.Services;

public class RuleBasedShippingService : IShippingService
{
    public (decimal charge, string eta, decimal chargeableWeightKg) Estimate(decimal totalWeightKg, string pincode, string paymentMethod)
    {
        var packagingWeight = 0.15m;
        var kg = totalWeightKg + packagingWeight;
        var chargeable = Math.Max(0.5m, Math.Ceiling(kg / 0.5m) * 0.5m);

        var zone = pincode.StartsWith("39") ? 0 : pincode.StartsWith("3") ? 1 : 2;
        decimal[] baseRate = { 49, 69, 89 };
        string[] etaByZone = { "2–4", "3–6", "5–8" };

        var charge = Math.Round(baseRate[zone] + Math.Max(0, chargeable - 0.5m) * 38);
        if (paymentMethod.Equals("Cod", StringComparison.OrdinalIgnoreCase)) charge += 35;

        return (charge, $"{etaByZone[zone]} business days", chargeable);
    }
}