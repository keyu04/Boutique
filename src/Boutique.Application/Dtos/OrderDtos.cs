namespace Boutique.Application.Dtos;

public record OrderRequest(
    List<OrderRequestItem> Items,
    string CustomerName, string Phone, string AddressLine,
    string City, string State, string Pincode,
    string PaymentMethod); // "Prepaid" or "Cod" — no prices here, ever

public record OrderRequestItem(int VariantId, int Quantity);

public record OrderResponse(
    string OrderNumber, string Status,
    decimal Subtotal, decimal ShippingCharge, decimal Total,
    string PaymentMethod, string PaymentStatus);