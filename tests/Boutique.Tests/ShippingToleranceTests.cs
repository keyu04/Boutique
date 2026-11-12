using Boutique.Application.Shipping;
using Xunit;

namespace Boutique.Tests;

public class ShippingToleranceTests
{
    [Fact] public void No_expected_price_is_accepted() => Assert.True(ShippingTolerance.Matches(null, 58m));
    [Fact] public void Same_price_matches() => Assert.True(ShippingTolerance.Matches(58m, 58m));
    [Fact] public void Within_one_rupee_matches() => Assert.True(ShippingTolerance.Matches(58m, 58.9m));
    [Fact] public void Different_price_is_rejected() => Assert.False(ShippingTolerance.Matches(58m, 78m));
}