using Boutique.Domain;
using Xunit;

namespace Boutique.Tests;

public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Pending,   OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending,   OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending,   OrderStatus.Packed,    false)]
    [InlineData(OrderStatus.Pending,   OrderStatus.Pending,   false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Packed,    true)]
    [InlineData(OrderStatus.Packed,    OrderStatus.Shipped,   true)]
    [InlineData(OrderStatus.Shipped,   OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Shipped,   OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    public void CanTransition_follows_the_design_table(OrderStatus from, OrderStatus to, bool expected) =>
        Assert.Equal(expected, OrderStatusRules.CanTransition(from, to));
}