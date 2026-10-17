using Boutique.Api;
using Xunit;

namespace Boutique.Tests;

public class CheckoutRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void Out_of_range_quantities_are_rejected(int quantity) =>
        Assert.NotNull(CheckoutRules.ValidateQuantities(new[] { quantity }));

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void In_range_quantities_are_accepted(int quantity) =>
        Assert.Null(CheckoutRules.ValidateQuantities(new[] { quantity }));

    [Theory]
    [InlineData("Prepaid")]
    [InlineData("cod")]
    public void Valid_payment_names_are_accepted(string text) =>
        Assert.True(CheckoutRules.TryParsePaymentMethod(text, out _));

    [Theory]
    [InlineData("99")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("Prepaid,Cod")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_payment_values_are_rejected(string? text) =>
        Assert.False(CheckoutRules.TryParsePaymentMethod(text, out _));
}