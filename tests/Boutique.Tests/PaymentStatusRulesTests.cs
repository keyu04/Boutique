using Boutique.Domain;

namespace Boutique.Tests;

public class PaymentStatusRulesTests
{
    [Theory]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Pending, OrderStatus.Pending, true)]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Pending, OrderStatus.Delivered, true)]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Paid, OrderStatus.Pending, false)]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Pending, OrderStatus.Cancelled, false)]
    [InlineData(PaymentMethod.Cod, PaymentStatus.CodDue, OrderStatus.Shipped, false)]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Failed, OrderStatus.Pending, false)]
    public void CanManuallyMarkAsPaid_only_allows_pending_prepaid_orders(
        PaymentMethod method, PaymentStatus paymentStatus, OrderStatus status, bool expected)
    {
        var order = new Order { PaymentMethod = method, PaymentStatus = paymentStatus, Status = status };

        Assert.Equal(expected, PaymentStatusRules.CanManuallyMarkAsPaid(order));
    }

    [Theory]
    [InlineData(PaymentMethod.Cod, PaymentStatus.CodDue, OrderStatus.Delivered, true)]
    [InlineData(PaymentMethod.Cod, PaymentStatus.CodDue, OrderStatus.Shipped, false)]
    [InlineData(PaymentMethod.Prepaid, PaymentStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(PaymentMethod.Cod, PaymentStatus.Paid, OrderStatus.Delivered, false)]
    public void ShouldMarkCodPaidOnDelivery_only_marks_due_cod_orders(
        PaymentMethod method, PaymentStatus paymentStatus, OrderStatus target, bool expected)
    {
        var order = new Order { PaymentMethod = method, PaymentStatus = paymentStatus };

        Assert.Equal(expected, PaymentStatusRules.ShouldMarkCodPaidOnDelivery(order, target));
    }
}
