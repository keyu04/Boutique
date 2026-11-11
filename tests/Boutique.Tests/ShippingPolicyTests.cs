using Boutique.Application.Shipping;
using Xunit;

namespace Boutique.Tests;

public class ShippingPolicyTests
{
    private static readonly ShippingPolicyOptions Opt = new()
    {
        StandardCharge = 49, MarginSharePercent = 40, MinProfit = 40,
        PrepaidDiscount = 20, CodFee = 0, CostBuffer = 10,
    };

    [Fact]
    public void Low_margin_bag_pays_the_floor_instead_of_the_standard_charge()
    {
        // margin 100, courier 100 + buffer 10 + min profit 40 - margin 100 = floor 50
        var r = ShippingPolicy.Calculate(new(200m, 100m, 100m, false), Opt);

        Assert.Equal(50m, r.Charge);
        Assert.Equal(50m, r.StandardCharge);
        Assert.Equal(0m, r.Saving);
        Assert.Equal(300m, r.FreeDeliveryAtSubtotal);
        Assert.Equal(100m, r.AmountToFreeDelivery);
    }

    [Fact]
    public void High_margin_bag_ships_free()
    {
        var r = ShippingPolicy.Calculate(new(400m, 200m, 100m, false), Opt);

        Assert.Equal(0m, r.Charge);
        Assert.Equal(49m, r.StandardCharge);
        Assert.Equal(49m, r.Saving);
        Assert.Equal(0m, r.AmountToFreeDelivery);
    }

    [Fact]
    public void Paying_online_is_cheaper_than_cash_on_delivery()
    {
        var cod = ShippingPolicy.Calculate(new(120m, 60m, 30m, true), Opt);
        var prepaid = ShippingPolicy.Calculate(new(120m, 60m, 30m, false), Opt);

        Assert.Equal(25m, cod.Charge);
        Assert.Equal(20m, prepaid.Charge);
    }

    [Fact]
    public void Every_order_keeps_at_least_the_minimum_profit()
    {
        foreach (var subtotal in new[] { 100m, 186m, 300m, 599m, 999m, 2999m })
        foreach (var costShare in new[] { 0.4m, 0.55m, 0.7m, 0.85m })
        foreach (var courier in new[] { 40m, 104m, 162m, 300m })
        foreach (var cod in new[] { true, false })
        {
            var itemCost = subtotal * costShare;
            var r = ShippingPolicy.Calculate(new(subtotal, itemCost, courier, cod), Opt);
            var profit = (subtotal - itemCost) + r.Charge - courier;

            Assert.True(profit >= Opt.MinProfit,
                $"subtotal {subtotal}, cost share {costShare}, courier {courier}, cod {cod}: profit {profit}");
            Assert.True(r.Charge <= r.StandardCharge, "discount must never raise the price");
        }
    }
}