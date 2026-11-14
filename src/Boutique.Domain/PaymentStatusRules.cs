namespace Boutique.Domain;

public static class PaymentStatusRules
{
    public static bool CanManuallyMarkAsPaid(Order order) =>
        order.PaymentMethod == PaymentMethod.Prepaid
        && order.PaymentStatus == PaymentStatus.Pending
        && order.Status != OrderStatus.Cancelled;

    public static bool ShouldMarkCodPaidOnDelivery(Order order, OrderStatus target) =>
        target == OrderStatus.Delivered
        && order.PaymentMethod == PaymentMethod.Cod
        && order.PaymentStatus == PaymentStatus.CodDue;
}
