namespace Boutique.Application.Shipping;

public class ShippingPolicyOptions
{
    public const string Section = "Shipping";

    public decimal StandardCharge { get; set; } = 49;
    public decimal MarginSharePercent { get; set; } = 40;   // max share of a bag's margin given back as shipping discount
    public decimal MinProfit { get; set; } = 40;            // every order must keep at least this after shipping
    public decimal PrepaidDiscount { get; set; } = 20;
    public decimal CodFee { get; set; } = 0;
    public decimal CostBuffer { get; set; } = 10;           // safety margin on the courier price
    public int HandlingDays { get; set; } = 1;             // packing time added to the courier's estimate
    public decimal PackagingWeightKg { get; set; } = 0.15m;
}

public record ShippingPolicyInput(decimal Subtotal, decimal ItemCost, decimal CourierCost, bool IsCod);

public record ShippingPolicyResult(
    decimal Charge, decimal StandardCharge, decimal Saving,
    decimal? FreeDeliveryAtSubtotal, decimal AmountToFreeDelivery);

public static class ShippingPolicy
{
    public static ShippingPolicyResult Calculate(ShippingPolicyInput input, ShippingPolicyOptions o)
    {
        var margin = Math.Max(0m, input.Subtotal - input.ItemCost);
        var cost = input.CourierCost + o.CostBuffer;                 // what we expect to pay the courier
        var floor = Math.Max(0m, cost + o.MinProfit - margin);       // lowest charge that still earns MinProfit
        var baseCharge = o.StandardCharge + (input.IsCod ? o.CodFee : 0m);
        var standard = Math.Max(baseCharge, floor);                  // the honest "no discount" price

        var share = Math.Clamp(o.MarginSharePercent, 0m, 100m) / 100m;
        var subsidy = Math.Min(standard, margin * share);
        var charge = Math.Max(floor, standard - subsidy);
        if (!input.IsCod) charge = Math.Max(floor, charge - o.PrepaidDiscount);

        charge = Math.Ceiling(charge);       // round up, never down: rounding must not cost you money
        standard = Math.Ceiling(standard);

        // The bag size at which delivery becomes free, assuming the bag keeps its current margin rate
        // and the same courier cost (adding heavy items can move this a little).
        decimal? freeAt = null;
        var marginRate = input.Subtotal > 0 ? margin / input.Subtotal : 0m;
        if (marginRate > 0 && share > 0)
        {
            var needed = baseCharge - (input.IsCod ? 0m : o.PrepaidDiscount);
            var needMargin = Math.Max(cost + o.MinProfit, needed / share);
            freeAt = Math.Ceiling(needMargin / marginRate);
        }

        var left = freeAt is null || charge == 0 ? 0m : Math.Max(0m, freeAt.Value - input.Subtotal);
        return new ShippingPolicyResult(charge, standard, standard - charge, freeAt, left);
    }
}